using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TodoList.Infrastructure.Reminders;

/// <summary>
/// Polls the reminder outbox and delivers due messages. A PostgreSQL advisory
/// lock serializes dispatch, so several app replicas can run the dispatcher
/// without sending the same reminder twice. Moving delivery to a dedicated
/// worker only changes the host that registers the service.
/// </summary>
public sealed class ReminderDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<ReminderOptions> options,
    ILogger<ReminderDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.Enabled)
        {
            logger.LogInformation("Reminder dispatcher is disabled.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, settings.PollIntervalSeconds));
        var batchSize = Math.Max(1, settings.BatchSize);

        logger.LogInformation(
            "Reminder dispatcher started with a {Interval} poll interval.",
            interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatchLock = scope.ServiceProvider.GetRequiredService<ReminderDispatchLock>();

                if (!await dispatchLock.TryAcquireAsync(stoppingToken))
                {
                    logger.LogDebug(
                        "Another replica holds the reminder dispatch lock; skipping this poll.");
                }
                else
                {
                    var delivery = scope.ServiceProvider.GetRequiredService<ReminderDeliveryService>();
                    var sent = await delivery.DispatchDueAsync(batchSize, stoppingToken);

                    if (sent > 0)
                    {
                        logger.LogInformation("Delivered {Count} reminder emails.", sent);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Reminder dispatch failed.");
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
