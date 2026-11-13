using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TodoList.Domain.Reminders;
using TodoList.Infrastructure.Persistence;

namespace TodoList.Infrastructure.Reminders;

/// <summary>
/// Deletes terminal reminders (<see cref="ReminderStatus.Sent"/>,
/// <see cref="ReminderStatus.Failed"/>, and <see cref="ReminderStatus.Cancelled"/>)
/// after the configured retention window. Pending reminders are never touched.
/// </summary>
public sealed class ReminderRetentionService(
    AppDbContext db,
    TimeProvider timeProvider,
    IOptions<ReminderOptions> options,
    ILogger<ReminderRetentionService> logger)
{
    public async Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        var retentionDays = Math.Max(1, options.Value.RetentionDays);
        var cutoff = timeProvider.GetUtcNow().AddDays(-retentionDays);

        var deleted = await db.Reminders
            .Where(reminder =>
                (reminder.Status == ReminderStatus.Sent
                    || reminder.Status == ReminderStatus.Failed
                    || reminder.Status == ReminderStatus.Cancelled)
                && (reminder.SentAtUtc < cutoff
                    || (reminder.SentAtUtc == null && reminder.CreatedAt < cutoff)))
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            logger.LogInformation(
                "Deleted {Count} terminal reminders older than {RetentionDays} days.",
                deleted,
                retentionDays);
        }

        return deleted;
    }
}
