namespace TodoList.Application.Tags;

public sealed record CreateTagRequest(string Name, string? Color = null);

public sealed record UpdateTagRequest(string Name, string? Color = null);
