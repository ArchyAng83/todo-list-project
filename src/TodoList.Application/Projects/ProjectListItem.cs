namespace TodoList.Application.Projects;

public sealed record ProjectListItem(
    Guid Id,
    string Name,
    string? Color,
    bool IsArchived,
    int SortOrder,
    int OpenTaskCount,
    int TotalTaskCount);
