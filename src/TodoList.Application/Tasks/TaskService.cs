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
    IValidator<CreateTaskRequest> createValidator,
    IValidator<UpdateTaskRequest> updateValidator,
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

        db.Tasks.Add(task);
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

        var now = timeProvider.GetUtcNow();
        task.Rename(request.Title, now);
        task.SetNotes(request.Notes, now);
        task.MoveToProject(request.ProjectId, now);
        task.SetPriority(request.Priority, now);
        task.SetDueDate(request.DueDate, request.DueTime, now);
        task.SetReminder(request.ReminderOffsetMinutes, now);

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SetStatusAsync(
        UserContext user,
        Guid taskId,
        TodoTaskStatus status,
        CancellationToken cancellationToken = default)
    {
        var task = await FindAsync(user, taskId, cancellationToken);

        if (task is null)
        {
            return Result.Failure(Error.NotFound("Task was not found."));
        }

        task.SetStatus(status, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
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
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
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
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(item.Id);
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
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
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
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
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
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
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
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
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
