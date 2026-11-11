namespace TodoList.Domain.Reminders;

public sealed class Reminder
{
    /// <summary>
    /// Delivery attempts before a reminder is treated as permanently failed.
    /// </summary>
    public const int MaxAttempts = 5;

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

    /// <summary>
    /// Records a failed attempt. Transient failures keep the reminder pending
    /// with an exponential back-off so the dispatcher picks it up again; the
    /// reminder becomes terminal <see cref="ReminderStatus.Failed"/> only after
    /// <see cref="MaxAttempts"/> attempts.
    /// </summary>
    public void MarkFailed(string error, DateTimeOffset now)
    {
        LastError = error;
        Attempts++;

        if (Attempts >= MaxAttempts)
        {
            Status = ReminderStatus.Failed;
            return;
        }

        Status = ReminderStatus.Pending;
        ScheduledAtUtc = now.Add(RetryDelay(Attempts));
    }

    private static TimeSpan RetryDelay(int attempt)
        => TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, attempt - 1)));

    public void Cancel()
    {
        if (Status == ReminderStatus.Sent)
        {
            return;
        }

        Status = ReminderStatus.Cancelled;
    }
}
