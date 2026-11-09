using TodoList.Domain.Tasks;

namespace TodoList.Application.Tasks;

public sealed record CreateTaskRequest(string Title, Guid? ProjectId = null);

public sealed record MoveTaskRequest(TodoTaskStatus Status, int TargetIndex);

public sealed record UpdateTaskRequest(
    string Title,
    string? Notes,
    Guid? ProjectId,
    TaskPriority Priority,
    DateOnly? DueDate,
    TimeOnly? DueTime,
    int? ReminderOffsetMinutes);

public sealed record AddChecklistItemRequest(string Title);

public sealed record SetChecklistItemCompletedRequest(bool IsCompleted);

public sealed record AddTaskTagRequest(Guid TagId);
