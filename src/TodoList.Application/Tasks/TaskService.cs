using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TodoList.Application.Common;
using TodoList.Application.Persistence;
using TodoList.Application.Tags;
using TodoList.Domain.Tasks;

namespace TodoList.Application.Tasks;

public sealed class TaskService(
    IAppDbContext db,
    TimeProvider timeProvider,
    TaskReminderScheduler reminderScheduler,
    IValidator<CreateTaskRequest> createValidator,
    IValidator<UpdateTaskRequest> updateValidator,
    IValidator<MoveTaskRequest> moveValidator,
    IValidator<AddChecklistItemRequest> checklistValidator,
    IValidator<AddTaskTagRequest> tagValidator)
{
    public async Task<Result<Guid>> CreateAsync(
        UserContext user,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            return Result.Failure<Guid>(ValidationErrors.From(validation));
        }

        if (!await ProjectBelongsToUserAsync(user, request.ProjectId, cancellationToken))
        {
            return Result.Failure<Guid>(Error.NotFound("Project was not found."));
        }

        var now = timeProvider.GetUtcNow();
        var task = TodoTask.Create(user.UserId, request.Title, now);
        task.MoveToProject(request.ProjectId, now);
        task.SetPriority(request.Priority, now);
        task.SetDueDate(request.DueDate, request.DueTime, now);
        task.SetReminder(request.ReminderOffsetMinutes, now);
        task.SetRecurrence(
            request.Recurrence,
            request.RecurrenceInterval,
            request.RecurrenceEndDate,
            now);

        db.Tasks.Add(task);
        await reminderScheduler.ReconcileAsync(task, user, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(task.Id);
    }

    public async Task<Result> UpdateAsync(
        UserContext user,
        Guid taskId,
        UpdateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            return Result.Failure(ValidationErrors.From(validation));
        }

        if (!await ProjectBelongsToUserAsync(user, request.ProjectId, cancellationToken))
        {
            return Result.Failure(Error.NotFound("Project was not found."));
        }

        var task = await FindAsync(user, taskId, cancellationToken);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("Task was not found."));
        }

        if (IsStale(task, request.Version))
        {
            return Result.Failure(ConcurrencyConflict());
        }

        var now = timeProvider.GetUtcNow();
        task.Rename(request.Title, now);
        task.SetNotes(request.Notes, now);
        task.MoveToProject(request.ProjectId, now);
        task.SetPriority(request.Priority, now);
        task.SetDueDate(request.DueDate, request.DueTime, now);
        task.SetReminder(request.ReminderOffsetMinutes, now);
        task.SetRecurrence(
            request.Recurrence,
            request.RecurrenceInterval,
            request.RecurrenceEndDate,
            now);

        await reminderScheduler.ReconcileAsync(task, user, cancellationToken);

        return await SaveAsync(cancellationToken);
    }

    /// <summary>
    /// Applies every change the task dialog makes — details, the status
    /// transition, and the tag set — in one <c>SaveChangesAsync</c> so a failure
    /// cannot leave the task half-updated.
    /// </summary>
    public async Task<Result> UpdateDetailsAsync(
        UserContext user,
        Guid taskId,
        UpdateTaskDetailsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(request.Status))
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.InvalidTaskStatus,
                "Unknown task status."));
        }

        var validation = await updateValidator.ValidateAsync(request.Details, cancellationToken);

        if (!validation.IsValid)
        {
            return Result.Failure(ValidationErrors.From(validation));
        }

        if (!await ProjectBelongsToUserAsync(user, request.Details.ProjectId, cancellationToken))
        {
            return Result.Failure(Error.NotFound("Project was not found."));
        }

        var task = await FindWithStructureAsync(user, taskId, cancellationToken);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("Task was not found."));
        }

        if (IsStale(task, request.Details.Version))
        {
            return Result.Failure(ConcurrencyConflict());
        }

        var tagIds = request.TagIds.Distinct().ToList();
        var tags = tagIds.Count == 0
            ? []
            : await db.Tags
                .Where(tag => tag.UserId == user.UserId && tagIds.Contains(tag.Id))
                .ToListAsync(cancellationToken);

        if (tags.Count != tagIds.Count)
        {
            return Result.Failure(Error.NotFound("Tag was not found."));
        }

        var now = timeProvider.GetUtcNow();
        var details = request.Details;

        task.Rename(details.Title, now);
        task.SetNotes(details.Notes, now);
        task.MoveToProject(details.ProjectId, now);
        task.SetPriority(details.Priority, now);
        task.SetDueDate(details.DueDate, details.DueTime, now);
        task.SetReminder(details.ReminderOffsetMinutes, now);
        task.SetRecurrence(
            details.Recurrence,
            details.RecurrenceInterval,
            details.RecurrenceEndDate,
            now);

        // Tags are applied before the status transition so a generated next
        // occurrence copies the new tag set instead of the old one.
        foreach (var link in task.Tags.Where(link => !tagIds.Contains(link.TagId)).ToList())
        {
            task.RemoveTag(link.TagId, now);
        }

        foreach (var tag in tags.Where(tag => task.Tags.All(link => link.TagId != tag.Id)))
        {
            task.AddTag(tag, now);
        }

        var previousStatus = task.Status;

        if (previousStatus != request.Status)
        {
            task.SetStatus(request.Status, now);

            if (previousStatus != TodoTaskStatus.Done && request.Status == TodoTaskStatus.Done)
            {
                var next = GenerateNextOccurrence(task, now);

                if (next is not null)
                {
                    await reminderScheduler.ReconcileAsync(next, user, cancellationToken);
                }
            }
        }

        await reminderScheduler.ReconcileAsync(task, user, cancellationToken);

        return await SaveAsync(cancellationToken);
    }

    public async Task<Result> SetStatusAsync(
        UserContext user,
        Guid taskId,
        TodoTaskStatus status,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status))
        {
            return Result.Failure(Error.Validation(
                ErrorCodes.InvalidTaskStatus,
                "Unknown task status."));
        }

        var task = await FindWithStructureAsync(user, taskId, cancellationToken);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("Task was not found."));
        }

        var now = timeProvider.GetUtcNow();
        var wasDone = task.Status == TodoTaskStatus.Done;
        task.SetStatus(status, now);

        var next = wasDone || status != TodoTaskStatus.Done
            ? null
            : GenerateNextOccurrence(task, now);

        await reminderScheduler.ReconcileAsync(task, user, cancellationToken);

        if (next is not null)
        {
            await reminderScheduler.ReconcileAsync(next, user, cancellationToken);
        }

        return await SaveAsync(cancellationToken);
    }

    public async Task<Result> MoveAsync(
        UserContext user,
        Guid taskId,
        MoveTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await moveValidator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            return Result.Failure(ValidationErrors.From(validation));
        }

        var task = await FindWithStructureAsync(user, taskId, cancellationToken);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("Task was not found."));
        }

        var now = timeProvider.GetUtcNow();

        // Only the source and target columns are affected. Load them in their
        // current visual order so the new position is stable.
        var affected = await db.Tasks
            .Where(candidate => candidate.UserId == user.UserId
                && (candidate.Status == task.Status || candidate.Status == request.Status))
            .OrderBy(candidate => candidate.SortOrder)
            .ThenBy(candidate => candidate.CreatedAt)
            .ToListAsync(cancellationToken);

        if (request.Status != task.Status)
        {
            Renumber(
                affected
                    .Where(candidate => candidate.Status == task.Status && candidate.Id != task.Id)
                    .ToList(),
                now);
        }

        var target = affected
            .Where(candidate => candidate.Status == request.Status && candidate.Id != task.Id)
            .ToList();

        var targetIndex = Math.Clamp(request.TargetIndex, 0, target.Count);
        target.Insert(targetIndex, task);

        var wasDone = task.Status == TodoTaskStatus.Done;
        task.SetStatus(request.Status, now);
        Renumber(target, now);

        var next = wasDone || request.Status != TodoTaskStatus.Done
            ? null
            : GenerateNextOccurrence(task, now);

        await reminderScheduler.ReconcileAsync(task, user, cancellationToken);

        if (next is not null)
        {
            await reminderScheduler.ReconcileAsync(next, user, cancellationToken);
        }

        return await SaveAsync(cancellationToken);
    }

    public async Task<Result> DeleteAsync(
        UserContext user,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await FindAsync(user, taskId, cancellationToken);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("Task was not found."));
        }

        db.Tasks.Remove(task);
        return await SaveAsync(cancellationToken);
    }

    public async Task<Result<Guid>> AddChecklistItemAsync(
        UserContext user,
        Guid taskId,
        AddChecklistItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await checklistValidator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            return Result.Failure<Guid>(ValidationErrors.From(validation));
        }

        var task = await FindWithChecklistAsync(user, taskId, cancellationToken);

        if (task is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Task was not found."));
        }

        var item = task.AddChecklistItem(request.Title, timeProvider.GetUtcNow());
        db.ChecklistItems.Add(item);

        var saved = await SaveAsync(cancellationToken);

        return saved.IsFailure ? Result.Failure<Guid>(saved.Error) : Result.Success(item.Id);
    }

    public async Task<Result> SetChecklistItemCompletedAsync(
        UserContext user,
        Guid taskId,
        Guid checklistItemId,
        SetChecklistItemCompletedRequest request,
        CancellationToken cancellationToken = default)
    {
        var task = await FindWithChecklistAsync(user, taskId, cancellationToken);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("Task was not found."));
        }

        var item = task.ChecklistItems.SingleOrDefault(x => x.Id == checklistItemId);

        if (item is null)
        {
            return Result.Failure(Error.NotFound("Checklist item was not found."));
        }

        item.SetCompleted(request.IsCompleted, timeProvider.GetUtcNow());
        return await SaveAsync(cancellationToken);
    }

    public async Task<Result> RemoveChecklistItemAsync(
        UserContext user,
        Guid taskId,
        Guid checklistItemId,
        CancellationToken cancellationToken = default)
    {
        var task = await FindWithChecklistAsync(user, taskId, cancellationToken);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("Task was not found."));
        }

        if (task.ChecklistItems.All(x => x.Id != checklistItemId))
        {
            return Result.Failure(Error.NotFound("Checklist item was not found."));
        }

        task.RemoveChecklistItem(checklistItemId, timeProvider.GetUtcNow());
        return await SaveAsync(cancellationToken);
    }

    public async Task<Result> AddTagAsync(
        UserContext user,
        Guid taskId,
        AddTaskTagRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await tagValidator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            return Result.Failure(ValidationErrors.From(validation));
        }

        var task = await FindWithTagsAsync(user, taskId, cancellationToken);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("Task was not found."));
        }

        var tag = await db.Tags.SingleOrDefaultAsync(
            x => x.Id == request.TagId && x.UserId == user.UserId,
            cancellationToken);

        if (tag is null)
        {
            return Result.Failure(Error.NotFound("Tag was not found."));
        }

        task.AddTag(tag, timeProvider.GetUtcNow());
        return await SaveAsync(cancellationToken);
    }

    public async Task<Result> RemoveTagAsync(
        UserContext user,
        Guid taskId,
        Guid tagId,
        CancellationToken cancellationToken = default)
    {
        var task = await FindWithTagsAsync(user, taskId, cancellationToken);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("Task was not found."));
        }

        task.RemoveTag(tagId, timeProvider.GetUtcNow());
        return await SaveAsync(cancellationToken);
    }

    private static Error ConcurrencyConflict()
        => Error.Conflict(
            ErrorCodes.ConcurrencyConflict,
            "The task was changed by another session. Reload it and try again.");

    private bool IsStale(TodoTask task, uint? expectedVersion)
        => expectedVersion is { } expected && db.GetVersion(task) != expected;

    private async Task<Result> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(ConcurrencyConflict());
        }
    }

    private static void Renumber(IReadOnlyList<TodoTask> tasks, DateTimeOffset now)
    {
        for (var index = 0; index < tasks.Count; index++)
        {
            tasks[index].SetSortOrder(index, now);
        }
    }

    private TodoTask? GenerateNextOccurrence(TodoTask task, DateTimeOffset now)
    {
        var next = task.CreateNextOccurrence(now);

        if (next is null)
        {
            return null;
        }

        next.CopyStructureFrom(task, now);
        db.Tasks.Add(next);

        return next;
    }

    private Task<TodoTask?> FindAsync(UserContext user, Guid taskId, CancellationToken cancellationToken)
        => db.Tasks.SingleOrDefaultAsync(
            task => task.Id == taskId && task.UserId == user.UserId,
            cancellationToken);

    private Task<TodoTask?> FindWithChecklistAsync(
        UserContext user,
        Guid taskId,
        CancellationToken cancellationToken)
        => db.Tasks
            .Include(task => task.ChecklistItems)
            .SingleOrDefaultAsync(
                task => task.Id == taskId && task.UserId == user.UserId,
                cancellationToken);

    private Task<TodoTask?> FindWithTagsAsync(
        UserContext user,
        Guid taskId,
        CancellationToken cancellationToken)
        => db.Tasks
            .Include(task => task.Tags)
            .SingleOrDefaultAsync(
                task => task.Id == taskId && task.UserId == user.UserId,
                cancellationToken);

    private Task<TodoTask?> FindWithStructureAsync(
        UserContext user,
        Guid taskId,
        CancellationToken cancellationToken)
        => db.Tasks
            .Include(task => task.ChecklistItems)
            .Include(task => task.Tags)
            .SingleOrDefaultAsync(
                task => task.Id == taskId && task.UserId == user.UserId,
                cancellationToken);

    private async Task<bool> ProjectBelongsToUserAsync(
        UserContext user,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        if (projectId is null)
        {
            return true;
        }

        return await db.Projects.AnyAsync(
            project => project.Id == projectId && project.UserId == user.UserId,
            cancellationToken);
    }
}
