using TodoList.Domain.Projects;
using TodoList.Domain.Tags;

namespace TodoList.Domain.Tasks;

public sealed class TodoTask
{
    public const int TitleMaxLength = 200;
    public const int NotesMaxLength = 4000;
    public const int MaxReminderOffsetMinutes = 60 * 24 * 7;
    public const int MaxRecurrenceInterval = 365;

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
        Recurrence = RecurrenceFrequency.None;
        RecurrenceInterval = 1;
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

    public RecurrenceFrequency Recurrence { get; private set; }

    public int RecurrenceInterval { get; private set; } = 1;

    public DateOnly? RecurrenceEndDate { get; private set; }

    /// <summary>
    /// Prevents a reopened and completed task from creating a second next
    /// occurrence. Resets when the schedule itself changes.
    /// </summary>
    public bool RecurrenceGenerated { get; private set; }

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
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), "Unknown task priority.");
        }

        Priority = priority;
        UpdatedAt = now;
    }

    public void SetStatus(TodoTaskStatus status, DateTimeOffset now)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Unknown task status.");
        }

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

        if (DueDate != dueDate || DueTime != dueTime)
        {
            RecurrenceGenerated = false;
        }

        DueDate = dueDate;
        DueTime = dueTime;

        if (dueDate is null)
        {
            ReminderOffsetMinutes = null;
            Recurrence = RecurrenceFrequency.None;
            RecurrenceInterval = 1;
            RecurrenceEndDate = null;
            RecurrenceGenerated = false;
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

    public void SetRecurrence(
        RecurrenceFrequency frequency,
        int interval,
        DateOnly? endDate,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(frequency))
        {
            throw new ArgumentOutOfRangeException(nameof(frequency), "Unknown recurrence frequency.");
        }

        if (frequency == RecurrenceFrequency.None)
        {
            Recurrence = RecurrenceFrequency.None;
            RecurrenceInterval = 1;
            RecurrenceEndDate = null;
            RecurrenceGenerated = false;
            UpdatedAt = now;
            return;
        }

        if (interval < 1 || interval > MaxRecurrenceInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                $"Recurrence interval must be between 1 and {MaxRecurrenceInterval}.");
        }

        if (DueDate is null)
        {
            throw new InvalidOperationException("A recurring task requires a due date.");
        }

        if (endDate is not null && endDate < DueDate)
        {
            throw new ArgumentException(
                "The recurrence end date cannot be before the due date.",
                nameof(endDate));
        }

        if (Recurrence != frequency || RecurrenceInterval != interval || RecurrenceEndDate != endDate)
        {
            RecurrenceGenerated = false;
        }

        Recurrence = frequency;
        RecurrenceInterval = interval;
        RecurrenceEndDate = endDate;
        UpdatedAt = now;
    }

    /// <summary>
    /// Creates the next occurrence of a recurring task, or returns <c>null</c>
    /// when the task does not recur, has no due date, already produced a next
    /// occurrence, or the next date would pass the recurrence end date.
    /// </summary>
    public TodoTask? CreateNextOccurrence(DateTimeOffset now)
    {
        if (Recurrence == RecurrenceFrequency.None || DueDate is null || RecurrenceGenerated)
        {
            return null;
        }

        var nextDueDate = Recurrence switch
        {
            RecurrenceFrequency.Daily => DueDate.Value.AddDays(RecurrenceInterval),
            RecurrenceFrequency.Weekly => DueDate.Value.AddDays(7 * RecurrenceInterval),
            RecurrenceFrequency.Monthly => DueDate.Value.AddMonths(RecurrenceInterval),
            _ => DueDate.Value
        };

        if (RecurrenceEndDate is not null && nextDueDate > RecurrenceEndDate)
        {
            return null;
        }

        var next = new TodoTask(Guid.CreateVersion7(), UserId, Title, now)
        {
            Notes = Notes,
            ProjectId = ProjectId,
            Priority = Priority,
            DueDate = nextDueDate,
            DueTime = DueTime,
            ReminderOffsetMinutes = ReminderOffsetMinutes,
            Recurrence = Recurrence,
            RecurrenceInterval = RecurrenceInterval,
            RecurrenceEndDate = RecurrenceEndDate
        };

        RecurrenceGenerated = true;
        UpdatedAt = now;

        return next;
    }

    /// <summary>
    /// Copies the tag links and checklist item titles from the previous
    /// occurrence. Checklist items start incomplete so the new occurrence is
    /// actionable; tag links stay attached because they describe the task.
    /// </summary>
    public void CopyStructureFrom(TodoTask source, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.UserId != UserId)
        {
            throw new InvalidOperationException("Cannot copy structure from another user's task.");
        }

        foreach (var link in source._tags)
        {
            if (_tags.All(existing => existing.TagId != link.TagId))
            {
                _tags.Add(new TaskTag(Id, link.TagId));
            }
        }

        foreach (var item in source._checklistItems.OrderBy(x => x.SortOrder))
        {
            _checklistItems.Add(ChecklistItem.Create(Id, item.Title, _checklistItems.Count, now));
        }

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
