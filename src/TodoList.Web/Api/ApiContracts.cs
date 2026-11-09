using TodoList.Domain.Tasks;

namespace TodoList.Web.Api;

public sealed record LoginBody(string Email, string Password);

public sealed record ProjectBody(string Name, string? Color = null);

public sealed record ArchiveProjectBody(bool IsArchived);

public sealed record TagBody(string Name, string? Color = null);

public sealed record CreateTaskBody(string Title, Guid? ProjectId = null);

public sealed record UpdateTaskBody(
    string Title,
    string? Notes,
    Guid? ProjectId,
    TaskPriority Priority,
    DateOnly? DueDate,
    TimeOnly? DueTime,
    int? ReminderOffsetMinutes);

public sealed record SetTaskStatusBody(TodoTaskStatus Status);

public sealed record AddChecklistItemBody(string Title);

public sealed record SetChecklistItemCompletedBody(bool IsCompleted);

public sealed record AddTagToTaskBody(Guid TagId);

public sealed record CreatedResponse(Guid Id);

public sealed record ProblemResponse(string Error);
