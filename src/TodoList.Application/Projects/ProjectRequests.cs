namespace TodoList.Application.Projects;

public sealed record CreateProjectRequest(string Name, string? Color = null);

public sealed record UpdateProjectRequest(string Name, string? Color);
