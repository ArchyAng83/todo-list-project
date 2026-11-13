using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TodoList.Infrastructure.Reminders;

/// <summary>
/// Runs <see cref="ReminderRetentionService"/> periodically. Cleanup is
/// idempotent, so every replica may run it without coordination.
/// </summary>
public sealed class ReminderRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ReminderOptions> options,
    ILogger<ReminderRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.Enabled)
        {
            logger.LogInformation("Reminder retention worker is disabled with the dispatcher.");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, settings.CleanupIntervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var retention = scope.ServiceProvider.GetRequiredService<ReminderRetentionService>();
                await retention.DeleteExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Reminder retention cleanup failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
