namespace TodoList.Application.Tags;

public sealed record TagRef(Guid Id, string Name, string Color);

public sealed record TagListItem(Guid Id, string Name, string Color, int TaskCount);
