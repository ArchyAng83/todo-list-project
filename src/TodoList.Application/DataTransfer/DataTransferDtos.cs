using TodoList.Domain.Tasks;

namespace TodoList.Application.DataTransfer;

/// <summary>
/// Versioned JSON document produced by the export endpoint and accepted by the
/// import endpoint. The version is part of the public contract, so breaking
/// changes to the shape must bump it and keep an importer for older versions.
/// </summary>
public sealed record ExportDocument(
    int Version,
    DateTimeOffset ExportedAt,
    IReadOnlyList<ExportProject> Projects,
    IReadOnlyList<ExportTag> Tags,
    IReadOnlyList<ExportTask> Tasks)
{
    public const int CurrentVersion = 1;
}

public sealed record ExportProject(
    Guid Id,
    string Name,
    string? Color,
    bool IsArchived,
    int SortOrder);

public sealed record ExportTag(
    Guid Id,
    string Name,
    string Color);

public sealed record ExportTask(
    Guid Id,
    Guid? ProjectId,
    string Title,
    string? Notes,
    TodoTaskStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate,
    TimeOnly? DueTime,
    int? ReminderOffsetMinutes,
    RecurrenceFrequency Recurrence,
    int RecurrenceInterval,
    DateOnly? RecurrenceEndDate,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<Guid> TagIds,
    IReadOnlyList<ExportChecklistItem> ChecklistItems);

public sealed record ExportChecklistItem(
    string Title,
    bool IsCompleted,
    int SortOrder);

public sealed record ImportSummary(
    int ProjectsCreated,
    int ProjectsReused,
    int TagsCreated,
    int TagsReused,
    int TasksCreated,
    int ChecklistItemsCreated);

public sealed record DataTransferSummary(
    int Projects,
    int Tags,
    int Tasks);
