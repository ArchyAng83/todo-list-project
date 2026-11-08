namespace TodoList.Domain.Tags;

public sealed class Tag
{
    public const int NameMaxLength = 50;
    public const string DefaultColor = "#6B7280";

    private Tag()
    {
        // Required by EF Core.
    }

    private Tag(Guid id, Guid userId, string name, string color, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        Name = name;
        Color = color;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Color { get; private set; } = DefaultColor;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Tag Create(Guid userId, string name, DateTimeOffset now, string? color = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        return new Tag(Guid.CreateVersion7(), userId, NormalizeName(name), NormalizeColor(color), now);
    }

    public void Rename(string name, DateTimeOffset now)
    {
        Name = NormalizeName(name);
        UpdatedAt = now;
    }

    public void SetColor(string? color, DateTimeOffset now)
    {
        Color = NormalizeColor(color);
        UpdatedAt = now;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tag name is required.", nameof(name));
        }

        var trimmed = name.Trim();

        if (trimmed.Length > NameMaxLength)
        {
            throw new ArgumentException($"Tag name cannot exceed {NameMaxLength} characters.", nameof(name));
        }

        return trimmed;
    }

    private static string NormalizeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return DefaultColor;
        }

        var trimmed = color.Trim();

        if (trimmed.Length != 7
            || trimmed[0] != '#'
            || !trimmed.Skip(1).All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Color must be a hex value such as #6B7280.", nameof(color));
        }

        return trimmed.ToUpperInvariant();
    }
}
