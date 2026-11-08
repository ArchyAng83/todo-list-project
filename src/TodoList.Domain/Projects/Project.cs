namespace TodoList.Domain.Projects;

public sealed class Project
{
    public const int NameMaxLength = 100;

    private Project()
    {
        // Required by EF Core.
    }

    private Project(Guid id, Guid userId, string name, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        Name = name;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Color { get; private set; }

    public bool IsArchived { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Project Create(Guid userId, string name, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        return new Project(Guid.CreateVersion7(), userId, NormalizeName(name), now);
    }

    public void Rename(string name, DateTimeOffset now)
    {
        Name = NormalizeName(name);
        UpdatedAt = now;
    }

    public void SetColor(string? color, DateTimeOffset now)
    {
        Color = string.IsNullOrWhiteSpace(color) ? null : color.Trim();
        UpdatedAt = now;
    }

    public void Archive(DateTimeOffset now)
    {
        if (IsArchived)
        {
            return;
        }

        IsArchived = true;
        UpdatedAt = now;
    }

    public void Unarchive(DateTimeOffset now)
    {
        if (!IsArchived)
        {
            return;
        }

        IsArchived = false;
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

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        var trimmed = name.Trim();

        if (trimmed.Length > NameMaxLength)
        {
            throw new ArgumentException($"Project name cannot exceed {NameMaxLength} characters.", nameof(name));
        }

        return trimmed;
    }
}
