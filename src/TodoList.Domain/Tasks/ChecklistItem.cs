namespace TodoList.Domain.Tasks;

public sealed class ChecklistItem
{
    public const int TitleMaxLength = 200;

    private ChecklistItem()
    {
        // Required by EF Core.
    }

    private ChecklistItem(Guid id, Guid taskId, string title, int sortOrder, DateTimeOffset now)
    {
        Id = id;
        TaskId = taskId;
        Title = title;
        SortOrder = sortOrder;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid TaskId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public bool IsCompleted { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal static ChecklistItem Create(Guid taskId, string title, int sortOrder, DateTimeOffset now)
    {
        return new ChecklistItem(Guid.CreateVersion7(), taskId, NormalizeTitle(title), sortOrder, now);
    }

    public void Rename(string title, DateTimeOffset now)
    {
        Title = NormalizeTitle(title);
        UpdatedAt = now;
    }

    public void Toggle(DateTimeOffset now)
    {
        IsCompleted = !IsCompleted;
        UpdatedAt = now;
    }

    public void SetCompleted(bool isCompleted, DateTimeOffset now)
    {
        if (IsCompleted == isCompleted)
        {
            return;
        }

        IsCompleted = isCompleted;
        UpdatedAt = now;
    }

    public void SetSortOrder(int sortOrder, DateTimeOffset now)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");
        }

        SortOrder = sortOrder;
        UpdatedAt = now;
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Checklist item title is required.", nameof(title));
        }

        var trimmed = title.Trim();

        if (trimmed.Length > TitleMaxLength)
        {
            throw new ArgumentException(
                $"Checklist item title cannot exceed {TitleMaxLength} characters.",
                nameof(title));
        }

        return trimmed;
    }
}
