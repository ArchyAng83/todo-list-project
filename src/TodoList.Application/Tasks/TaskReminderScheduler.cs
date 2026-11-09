using Microsoft.EntityFrameworkCore;
using TodoList.Application.Common;
using TodoList.Application.Persistence;
using TodoList.Domain.Reminders;
using TodoList.Domain.Tasks;

namespace TodoList.Application.Tasks;

/// <summary>
/// Keeps the database-backed reminder outbox in sync with a task. A task has
/// at most one pending reminder; editing the due date or the offset replaces
/// it, and completing the task cancels it.
/// </summary>
public sealed class TaskReminderScheduler(IAppDbContext db, TimeProvider timeProvider)
{
    private static readonly TimeOnly DefaultReminderTime = new(9, 0);

    public async Task ReconcileAsync(
        TodoTask task,
        UserContext user,
        CancellationToken cancellationToken = default)
    {
        var pending = await db.Reminders
            .Where(reminder => reminder.TaskId == task.Id
                && reminder.Status == ReminderStatus.Pending)
            .ToListAsync(cancellationToken);

        var scheduledAt = ComputeScheduledAt(task, user);

        if (scheduledAt is null || task.Status == TodoTaskStatus.Done)
        {
            foreach (var reminder in pending)
            {
                reminder.Cancel();
            }

            return;
        }

        if (pending.Count == 1 && pending[0].ScheduledAtUtc == scheduledAt.Value)
        {
            return;
        }

        foreach (var reminder in pending)
        {
            reminder.Cancel();
        }

        db.Reminders.Add(Reminder.Create(
            user.UserId,
            task.Id,
            scheduledAt.Value,
            timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// Reminders use the due time, or 09:00 local time for date-only tasks so
    /// a reminder is not delivered at midnight.
    /// </summary>
    public DateTimeOffset? ComputeScheduledAt(TodoTask task, UserContext user)
    {
        if (task.DueDate is null || task.ReminderOffsetMinutes is null)
        {
            return null;
        }

        var dueUtc = UserTime.ToUtc(task.DueDate.Value, task.DueTime ?? DefaultReminderTime, user.TimeZoneId);

        return dueUtc.AddMinutes(-task.ReminderOffsetMinutes.Value);
    }
}
