using TodoList.Application.Tags;
using TodoList.Domain.Tasks;

namespace TodoList.Application.Tasks;

public sealed record ChecklistItemDto(Guid Id, string Title, bool IsCompleted, int SortOrder);

public sealed record TaskListItem(
    Guid Id,
    string Title,
    TodoTaskStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate,
    TimeOnly? DueTime,
    Guid? ProjectId,
    string? ProjectName,
    IReadOnlyList<TagRef> Tags,
    int ChecklistTotal,
    int ChecklistCompleted);

public sealed record TaskDetails(
    Guid Id,
    string Title,
    string? Notes,
    TodoTaskStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate,
    TimeOnly? DueTime,
    int? ReminderOffsetMinutes,
    Guid? ProjectId,
    string? ProjectName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<ChecklistItemDto> ChecklistItems,
    IReadOnlyList<TagRef> Tags);

public sealed record TaskListFilter(
    TodoTaskStatus? Status = null,
    Guid? ProjectId = null,
    Guid? TagId = null,
    string? Search = null,
    TaskView View = TaskView.All,
    TaskSort Sort = TaskSort.Default);
