namespace TodoList.Domain.Reminders;

public sealed class Reminder
{
    private Reminder()
    {
        // Required by EF Core.
    }

    private Reminder(Guid id, Guid userId, Guid taskId, DateTimeOffset scheduledAtUtc, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        TaskId = taskId;
        ScheduledAtUtc = scheduledAtUtc;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid TaskId { get; private set; }

    public DateTimeOffset ScheduledAtUtc { get; private set; }

    public ReminderStatus Status { get; private set; } = ReminderStatus.Pending;

    public int Attempts { get; private set; }

    public DateTimeOffset? SentAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Reminder Create(Guid userId, Guid taskId, DateTimeOffset scheduledAtUtc, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("Task id is required.", nameof(taskId));
        }

        return new Reminder(Guid.CreateVersion7(), userId, taskId, scheduledAtUtc, now);
    }

    public void MarkSent(DateTimeOffset now)
    {
        Status = ReminderStatus.Sent;
        SentAtUtc = now;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Status = ReminderStatus.Failed;
        Attempts++;
        LastError = error;
    }

    public void Cancel()
    {
        if (Status == ReminderStatus.Sent)
        {
            return;
        }

        Status = ReminderStatus.Cancelled;
    }
}
