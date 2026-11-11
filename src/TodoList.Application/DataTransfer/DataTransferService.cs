using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using TodoList.Application.Common;
using TodoList.Application.Persistence;
using TodoList.Application.Tasks;
using TodoList.Domain.Projects;
using TodoList.Domain.Tags;
using TodoList.Domain.Tasks;

namespace TodoList.Application.DataTransfer;

/// <summary>
/// Exports the signed-in user's data as a versioned JSON document or a flat
/// CSV file, and imports a JSON document with merge semantics: projects and
/// tags are reused by name, while every imported task is created as a new
/// task owned by the current user.
/// </summary>
public sealed class DataTransferService(
    IAppDbContext db,
    TimeProvider timeProvider,
    TaskReminderScheduler reminderScheduler)
{
    public async Task<ExportDocument> ExportAsync(
        UserContext user,
        CancellationToken cancellationToken = default)
    {
        var projects = await db.Projects
            .AsNoTracking()
            .Where(project => project.UserId == user.UserId)
            .OrderBy(project => project.SortOrder)
            .ThenBy(project => project.Name)
            .ToListAsync(cancellationToken);

        var tags = await db.Tags
            .AsNoTracking()
            .Where(tag => tag.UserId == user.UserId)
            .OrderBy(tag => tag.Name)
            .ToListAsync(cancellationToken);

        var tasks = await db.Tasks
            .AsNoTracking()
            .Where(task => task.UserId == user.UserId)
            .Include(task => task.Tags)
            .ThenInclude(link => link.Tag)
            .Include(task => task.ChecklistItems)
            .OrderBy(task => task.SortOrder)
            .ThenBy(task => task.CreatedAt)
            .ToListAsync(cancellationToken);

        return new ExportDocument(
            ExportDocument.CurrentVersion,
            timeProvider.GetUtcNow(),
            projects
                .Select(project => new ExportProject(
                    project.Id,
                    project.Name,
                    project.Color,
                    project.IsArchived,
                    project.SortOrder))
                .ToList(),
            tags
                .Select(tag => new ExportTag(tag.Id, tag.Name, tag.Color))
                .ToList(),
            tasks
                .Select(task => new ExportTask(
                    task.Id,
                    task.ProjectId,
                    task.Title,
                    task.Notes,
                    task.Status,
                    task.Priority,
                    task.DueDate,
                    task.DueTime,
                    task.ReminderOffsetMinutes,
                    task.Recurrence,
                    task.RecurrenceInterval,
                    task.RecurrenceEndDate,
                    task.SortOrder,
                    task.CreatedAt,
                    task.CompletedAt,
                    task.Tags.Select(link => link.TagId).ToList(),
                    task.ChecklistItems
                        .OrderBy(item => item.SortOrder)
                        .Select(item => new ExportChecklistItem(
                            item.Title,
                            item.IsCompleted,
                            item.SortOrder))
                        .ToList()))
                .ToList());
    }

    public async Task<string> ExportTasksCsvAsync(
        UserContext user,
        CancellationToken cancellationToken = default)
    {
        var tasks = await db.Tasks
            .AsNoTracking()
            .Where(task => task.UserId == user.UserId)
            .Include(task => task.Project)
            .Include(task => task.Tags)
            .ThenInclude(link => link.Tag)
            .Include(task => task.ChecklistItems)
            .OrderBy(task => task.SortOrder)
            .ThenBy(task => task.CreatedAt)
            .ToListAsync(cancellationToken);

        var rows = tasks
            .Select(task => new TaskCsvRow(
                task.Id,
                task.Title,
                task.Status,
                task.Priority,
                task.Project?.Name,
                task.DueDate,
                task.DueTime,
                task.ReminderOffsetMinutes,
                task.Recurrence,
                task.RecurrenceInterval,
                task.RecurrenceEndDate,
                task.CreatedAt,
                task.CompletedAt,
                task.Tags
                    .OrderBy(link => link.Tag.Name)
                    .Select(link => link.Tag.Name)
                    .ToList(),
                task.ChecklistItems
                    .OrderBy(item => item.SortOrder)
                    .Select(item => new TaskCsvChecklistItem(item.Title, item.IsCompleted))
                    .ToList()))
            .ToList();

        return TaskCsvExport.Build(rows);
    }

    public async Task<DataTransferSummary> GetSummaryAsync(
        UserContext user,
        CancellationToken cancellationToken = default)
    {
        var projects = await db.Projects
            .CountAsync(project => project.UserId == user.UserId, cancellationToken);
        var tags = await db.Tags
            .CountAsync(tag => tag.UserId == user.UserId, cancellationToken);
        var tasks = await db.Tasks
            .CountAsync(task => task.UserId == user.UserId, cancellationToken);

        return new DataTransferSummary(projects, tags, tasks);
    }

    public async Task<Result<ImportSummary>> ImportAsync(
        UserContext user,
        ExportDocument document,
        CancellationToken cancellationToken = default)
    {
        if (document.Version != ExportDocument.CurrentVersion)
        {
            return Result.Failure<ImportSummary>(Error.Validation(
                ErrorCodes.ImportInvalid,
                $"Unsupported export version {document.Version}. "
                + $"The supported version is {ExportDocument.CurrentVersion}."));
        }

        if (document.Projects is null || document.Tags is null || document.Tasks is null)
        {
            return Result.Failure<ImportSummary>(Error.Validation(
                ErrorCodes.ImportInvalid,
                "The document must contain projects, tags, and tasks arrays."));
        }

        var validation = ValidateImportDocument(document);

        if (!validation.IsValid)
        {
            return Result.Failure<ImportSummary>(ValidationErrors.From(validation));
        }

        try
        {
            return await ImportCoreAsync(user, document, cancellationToken);
        }
        catch (Exception exception) when (exception is
            ArgumentException or InvalidOperationException or DbUpdateException)
        {
            return Result.Failure<ImportSummary>(Error.Validation(
                ErrorCodes.ImportInvalid,
                $"Import failed: {exception.Message}"));
        }
    }

    private async Task<Result<ImportSummary>> ImportCoreAsync(
        UserContext user,
        ExportDocument document,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var existingProjects = await db.Projects
            .Where(project => project.UserId == user.UserId)
            .ToListAsync(cancellationToken);
        var projectsByName = new Dictionary<string, Project>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in existingProjects)
        {
            projectsByName.TryAdd(project.Name, project);
        }

        var projectIds = new Dictionary<Guid, Guid>();
        var projectsCreated = 0;
        var projectsReused = 0;

        foreach (var imported in document.Projects)
        {
            if (imported is null)
            {
                return Result.Failure<ImportSummary>(Error.Validation(
                    ErrorCodes.ImportInvalid,
                    "The document contains an empty project entry."));
            }

            var key = imported.Name.Trim();

            if (projectsByName.TryGetValue(key, out var existing))
            {
                projectIds[imported.Id] = existing.Id;
                projectsReused++;
                continue;
            }

            var project = Project.Create(user.UserId, imported.Name, now);
            project.SetColor(imported.Color, now);
            project.SetSortOrder(imported.SortOrder, now);

            if (imported.IsArchived)
            {
                project.Archive(now);
            }

            db.Projects.Add(project);
            projectsByName[key] = project;
            projectIds[imported.Id] = project.Id;
            projectsCreated++;
        }

        var existingTags = await db.Tags
            .Where(tag => tag.UserId == user.UserId)
            .ToListAsync(cancellationToken);
        var tagsByName = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in existingTags)
        {
            tagsByName.TryAdd(tag.Name, tag);
        }

        var tagIds = new Dictionary<Guid, Guid>();
        var tagsById = new Dictionary<Guid, Tag>();
        var tagsCreated = 0;
        var tagsReused = 0;

        foreach (var imported in document.Tags)
        {
            if (imported is null)
            {
                return Result.Failure<ImportSummary>(Error.Validation(
                    ErrorCodes.ImportInvalid,
                    "The document contains an empty tag entry."));
            }

            var key = imported.Name.Trim();

            if (tagsByName.TryGetValue(key, out var existing))
            {
                tagIds[imported.Id] = existing.Id;
                tagsById[existing.Id] = existing;
                tagsReused++;
                continue;
            }

            var tag = Tag.Create(user.UserId, imported.Name, now, imported.Color);
            db.Tags.Add(tag);
            tagsByName[key] = tag;
            tagIds[imported.Id] = tag.Id;
            tagsById[tag.Id] = tag;
            tagsCreated++;
        }

        var checklistItemsCreated = 0;

        foreach (var imported in document.Tasks)
        {
            if (imported is null)
            {
                return Result.Failure<ImportSummary>(Error.Validation(
                    ErrorCodes.ImportInvalid,
                    "The document contains an empty task entry."));
            }

            if (!Enum.IsDefined(imported.Status))
            {
                return Result.Failure<ImportSummary>(Error.Validation(
                    ErrorCodes.ImportInvalid,
                    $"Task '{imported.Title}' has an unknown status."));
            }

            if (!Enum.IsDefined(imported.Priority))
            {
                return Result.Failure<ImportSummary>(Error.Validation(
                    ErrorCodes.ImportInvalid,
                    $"Task '{imported.Title}' has an unknown priority."));
            }

            Guid? projectId = null;

            if (imported.ProjectId is { } importedProjectId)
            {
                if (!projectIds.TryGetValue(importedProjectId, out var mappedProjectId))
                {
                    return Result.Failure<ImportSummary>(Error.Validation(
                        ErrorCodes.ImportInvalid,
                        $"Task '{imported.Title}' references a project that is not part of the import."));
                }

                projectId = mappedProjectId;
            }

            var task = TodoTask.Create(user.UserId, imported.Title, now);
            task.SetNotes(imported.Notes, now);
            task.MoveToProject(projectId, now);
            task.SetPriority(imported.Priority, now);
            task.SetDueDate(imported.DueDate, imported.DueTime, now);
            task.SetReminder(imported.ReminderOffsetMinutes, now);
            task.SetRecurrence(
                imported.Recurrence,
                imported.RecurrenceInterval,
                imported.RecurrenceEndDate,
                now);
            task.SetSortOrder(imported.SortOrder, now);
            task.SetStatus(imported.Status, now);

            foreach (var importedTagId in imported.TagIds ?? [])
            {
                if (!tagIds.TryGetValue(importedTagId, out var mappedTagId)
                    || !tagsById.TryGetValue(mappedTagId, out var tag))
                {
                    return Result.Failure<ImportSummary>(Error.Validation(
                        ErrorCodes.ImportInvalid,
                        $"Task '{imported.Title}' references a tag that is not part of the import."));
                }

                task.AddTag(tag, now);
            }

            foreach (var importedItem in (imported.ChecklistItems ?? []).OrderBy(item => item.SortOrder))
            {
                var item = task.AddChecklistItem(importedItem.Title, now);
                item.SetCompleted(importedItem.IsCompleted, now);
                checklistItemsCreated++;
            }

            db.Tasks.Add(task);

            if (task.ReminderOffsetMinutes is not null && task.Status != TodoTaskStatus.Done)
            {
                await reminderScheduler.ReconcileAsync(task, user, cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new ImportSummary(
            projectsCreated,
            projectsReused,
            tagsCreated,
            tagsReused,
            document.Tasks.Count,
            checklistItemsCreated));
    }

    /// <summary>
    /// Validates every imported project, tag, and task <i>before</i> any entity
    /// is added to the change tracker, so a bad document fails fast with a
    /// single 400 instead of leaving a partially applied import or surfacing a
    /// 500 from EF Core.
    /// </summary>
    private static ValidationResult ValidateImportDocument(ExportDocument document)
    {
        var errors = new List<ValidationFailure>();

        for (var i = 0; i < document.Projects.Count; i++)
        {
            var project = document.Projects[i];
            var prefix = $"Projects[{i}]";

            if (string.IsNullOrWhiteSpace(project.Name))
            {
                errors.Add(new ValidationFailure($"{prefix}.Name", "Project name is required.")
                { ErrorCode = ValidationCodes.Required });
            }
            else if (project.Name.Trim().Length > Project.NameMaxLength)
            {
                errors.Add(new ValidationFailure($"{prefix}.Name",
                    $"Project name cannot exceed {Project.NameMaxLength} characters.")
                { ErrorCode = ValidationCodes.TooLong });
            }

            if (!string.IsNullOrWhiteSpace(project.Color) && !IsValidHexColor(project.Color))
            {
                errors.Add(new ValidationFailure($"{prefix}.Color",
                    "Color must be a hex value such as #6B7280.")
                { ErrorCode = ValidationCodes.InvalidColor });
            }
        }

        for (var i = 0; i < document.Tags.Count; i++)
        {
            var tag = document.Tags[i];
            var prefix = $"Tags[{i}]";

            if (string.IsNullOrWhiteSpace(tag.Name))
            {
                errors.Add(new ValidationFailure($"{prefix}.Name", "Tag name is required.")
                { ErrorCode = ValidationCodes.Required });
            }
            else if (tag.Name.Trim().Length > Tag.NameMaxLength)
            {
                errors.Add(new ValidationFailure($"{prefix}.Name",
                    $"Tag name cannot exceed {Tag.NameMaxLength} characters.")
                { ErrorCode = ValidationCodes.TooLong });
            }

            if (!string.IsNullOrWhiteSpace(tag.Color) && !IsValidHexColor(tag.Color))
            {
                errors.Add(new ValidationFailure($"{prefix}.Color",
                    "Color must be a hex value such as #6B7280.")
                { ErrorCode = ValidationCodes.InvalidColor });
            }
        }

        for (var i = 0; i < document.Tasks.Count; i++)
        {
            var task = document.Tasks[i];
            var prefix = $"Tasks[{i}]";

            if (string.IsNullOrWhiteSpace(task.Title))
            {
                errors.Add(new ValidationFailure($"{prefix}.Title", "Task title is required.")
                { ErrorCode = ValidationCodes.Required });
            }
            else if (task.Title.Trim().Length > TodoTask.TitleMaxLength)
            {
                errors.Add(new ValidationFailure($"{prefix}.Title",
                    $"Task title cannot exceed {TodoTask.TitleMaxLength} characters.")
                { ErrorCode = ValidationCodes.TooLong });
            }

            if (!Enum.IsDefined(task.Status))
            {
                errors.Add(new ValidationFailure($"{prefix}.Status", "Unknown task status.")
                { ErrorCode = ValidationCodes.InvalidEnum });
            }

            if (!Enum.IsDefined(task.Priority))
            {
                errors.Add(new ValidationFailure($"{prefix}.Priority", "Unknown task priority.")
                { ErrorCode = ValidationCodes.InvalidEnum });
            }

            if (!Enum.IsDefined(task.Recurrence))
            {
                errors.Add(new ValidationFailure($"{prefix}.Recurrence", "Unknown recurrence frequency.")
                { ErrorCode = ValidationCodes.InvalidEnum });
            }

            if (task.DueTime is not null && task.DueDate is null)
            {
                errors.Add(new ValidationFailure($"{prefix}.DueTime", "A due time requires a due date.")
                { ErrorCode = ValidationCodes.DueTimeRequiresDueDate });
            }

            if (task.ReminderOffsetMinutes is not null
                && (task.ReminderOffsetMinutes < 0 || task.ReminderOffsetMinutes > TodoTask.MaxReminderOffsetMinutes))
            {
                errors.Add(new ValidationFailure($"{prefix}.ReminderOffsetMinutes",
                    $"Reminder offset must be between 0 and {TodoTask.MaxReminderOffsetMinutes} minutes.")
                { ErrorCode = ValidationCodes.OutOfRange });
            }

            if (task.ReminderOffsetMinutes is not null && task.DueDate is null)
            {
                errors.Add(new ValidationFailure($"{prefix}.ReminderOffsetMinutes", "A reminder requires a due date.")
                { ErrorCode = ValidationCodes.ReminderRequiresDueDate });
            }

            if (task.Recurrence != RecurrenceFrequency.None && task.DueDate is null)
            {
                errors.Add(new ValidationFailure($"{prefix}.Recurrence", "A recurring task requires a due date.")
                { ErrorCode = ValidationCodes.RecurrenceRequiresDueDate });
            }

            if (task.Recurrence != RecurrenceFrequency.None
                && (task.RecurrenceInterval < 1 || task.RecurrenceInterval > TodoTask.MaxRecurrenceInterval))
            {
                errors.Add(new ValidationFailure($"{prefix}.RecurrenceInterval",
                    $"Recurrence interval must be between 1 and {TodoTask.MaxRecurrenceInterval}.")
                { ErrorCode = ValidationCodes.OutOfRange });
            }

            if (task.RecurrenceEndDate is not null && task.DueDate is not null
                && task.RecurrenceEndDate < task.DueDate)
            {
                errors.Add(new ValidationFailure($"{prefix}.RecurrenceEndDate",
                    "The recurrence end date cannot be before the due date.")
                { ErrorCode = ValidationCodes.RecurrenceEndBeforeDue });
            }
        }

        return new ValidationResult(errors);
    }

    private static bool IsValidHexColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return true;
        }

        var trimmed = color.Trim();

        return trimmed.Length == 7
            && trimmed[0] == '#'
            && trimmed.Skip(1).All(Uri.IsHexDigit);
    }
}
