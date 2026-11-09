using TodoList.Domain.Projects;
using TodoList.Domain.Tags;

namespace TodoList.Domain.Tasks;

public sealed class TodoTask
{
    public const int TitleMaxLength = 200;
    public const int NotesMaxLength = 4000;
    public const int MaxReminderOffsetMinutes = 60 * 24 * 7;

    private readonly List<ChecklistItem> _checklistItems = [];
    private readonly List<TaskTag> _tags = [];

    private TodoTask()
    {
        // Required by EF Core.
    }

    private TodoTask(Guid id, Guid userId, string title, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        Title = title;
        Status = TodoTaskStatus.Todo;
        Priority = TaskPriority.None;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid? ProjectId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    public TodoTaskStatus Status { get; private set; }

    public TaskPriority Priority { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public TimeOnly? DueTime { get; private set; }

    public int? ReminderOffsetMinutes { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Project? Project { get; private set; }

    public IReadOnlyCollection<ChecklistItem> ChecklistItems => _checklistItems;

    public IReadOnlyCollection<TaskTag> Tags => _tags;

    public static TodoTask Create(Guid userId, string title, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        return new TodoTask(Guid.CreateVersion7(), userId, NormalizeTitle(title), now);
    }

    public void Rename(string title, DateTimeOffset now)
    {
        Title = NormalizeTitle(title);
        UpdatedAt = now;
    }

    public void SetNotes(string? notes, DateTimeOffset now)
    {
        if (notes is not null && notes.Length > NotesMaxLength)
        {
            throw new ArgumentException($"Notes cannot exceed {NotesMaxLength} characters.", nameof(notes));
        }

        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAt = now;
    }

    public void SetPriority(TaskPriority priority, DateTimeOffset now)
    {
        Priority = priority;
        UpdatedAt = now;
    }

    public void SetStatus(TodoTaskStatus status, DateTimeOffset now)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;
        CompletedAt = status == TodoTaskStatus.Done ? now : null;
        UpdatedAt = now;
    }

    public void SetDueDate(DateOnly? dueDate, TimeOnly? dueTime, DateTimeOffset now)
    {
        if (dueTime is not null && dueDate is null)
        {
            throw new ArgumentException("A due time requires a due date.", nameof(dueTime));
        }

        DueDate = dueDate;
        DueTime = dueTime;

        if (dueDate is null)
        {
            ReminderOffsetMinutes = null;
        }

        UpdatedAt = now;
    }

    public void SetReminder(int? offsetMinutes, DateTimeOffset now)
    {
        if (offsetMinutes is not null)
        {
            if (DueDate is null)
            {
                throw new InvalidOperationException("A reminder requires a due date.");
            }

            if (offsetMinutes < 0 || offsetMinutes > MaxReminderOffsetMinutes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offsetMinutes),
                    $"Reminder offset must be between 0 and {MaxReminderOffsetMinutes} minutes.");
            }
        }

        ReminderOffsetMinutes = offsetMinutes;
        UpdatedAt = now;
    }

    public void MoveToProject(Guid? projectId, DateTimeOffset now)
    {
        ProjectId = projectId;
        UpdatedAt = now;
    }

    public void SetSortOrder(int sortOrder, DateTimeOffset now)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");
        }

        if (SortOrder == sortOrder)
        {
            return;
        }

        SortOrder = sortOrder;
        UpdatedAt = now;
    }

    public ChecklistItem AddChecklistItem(string title, DateTimeOffset now)
    {
        var item = ChecklistItem.Create(Id, title, _checklistItems.Count, now);
        _checklistItems.Add(item);
        UpdatedAt = now;
        return item;
    }

    public void RemoveChecklistItem(Guid checklistItemId, DateTimeOffset now)
    {
        var item = _checklistItems.FirstOrDefault(x => x.Id == checklistItemId)
            ?? throw new InvalidOperationException("Checklist item was not found on this task.");

        _checklistItems.Remove(item);
        UpdatedAt = now;
    }

    public void AddTag(Tag tag, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(tag);

        if (tag.UserId != UserId)
        {
            throw new InvalidOperationException("Cannot attach a tag owned by another user.");
        }

        if (_tags.Any(x => x.TagId == tag.Id))
        {
            return;
        }

        _tags.Add(new TaskTag(Id, tag.Id));
        UpdatedAt = now;
    }

    public void RemoveTag(Guid tagId, DateTimeOffset now)
    {
        var link = _tags.FirstOrDefault(x => x.TagId == tagId);

        if (link is null)
        {
            return;
        }

        _tags.Remove(link);
        UpdatedAt = now;
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Task title is required.", nameof(title));
        }

        var trimmed = title.Trim();

        if (trimmed.Length > TitleMaxLength)
        {
            throw new ArgumentException($"Task title cannot exceed {TitleMaxLength} characters.", nameof(title));
        }

        return trimmed;
    }
}
