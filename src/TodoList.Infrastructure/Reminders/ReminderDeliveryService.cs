using System.Globalization;
using System.Net;
using System.Resources;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TodoList.Application.Email;
using TodoList.Domain.Reminders;
using TodoList.Domain.Tasks;
using TodoList.Infrastructure.Identity;
using TodoList.Infrastructure.Persistence;
using TodoList.Infrastructure.Resources.Email;

namespace TodoList.Infrastructure.Reminders;

/// <summary>
/// Delivers due reminders from the database-backed outbox. A reminder is
/// marked <see cref="ReminderStatus.Sent"/> only after the sender succeeds;
/// failed sends become terminal <see cref="ReminderStatus.Failed"/> records
/// with the error message, which keeps delivery at-least-once for the retry
/// phase to build on later.
/// </summary>
public sealed class ReminderDeliveryService(
    AppDbContext db,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    ILogger<ReminderDeliveryService> logger)
{
    private static readonly ResourceManager Resources = new(
        "TodoList.Infrastructure.Resources.Email.ReminderEmailResources",
        typeof(ReminderEmailResources).Assembly);

    public async Task<int> DispatchDueAsync(
        int batchSize = 25,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var due = await db.Reminders
            .Where(reminder => reminder.Status == ReminderStatus.Pending
                && reminder.ScheduledAtUtc <= now)
            .OrderBy(reminder => reminder.ScheduledAtUtc)
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return 0;
        }

        var taskIds = due.Select(reminder => reminder.TaskId).Distinct().ToList();
        var userIds = due.Select(reminder => reminder.UserId).Distinct().ToList();

        var tasks = await db.Tasks
            .AsNoTracking()
            .Include(task => task.Project)
            .Where(task => taskIds.Contains(task.Id))
            .ToDictionaryAsync(task => task.Id, cancellationToken);

        var users = await db.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        var sent = 0;

        foreach (var reminder in due)
        {
            if (!tasks.TryGetValue(reminder.TaskId, out var task)
                || task.Status == TodoTaskStatus.Done
                || !users.TryGetValue(reminder.UserId, out var user)
                || string.IsNullOrWhiteSpace(user.Email))
            {
                reminder.Cancel();
                continue;
            }

            try
            {
                var culture = ResolveCulture(user.Culture);
                var message = BuildMessage(task, user, culture);

                await emailSender.SendAsync(message, cancellationToken);

                reminder.MarkSent(now);
                sent++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                reminder.MarkFailed(Truncate(exception.Message));
                logger.LogError(
                    exception,
                    "Reminder {ReminderId} for task {TaskId} could not be sent.",
                    reminder.Id,
                    reminder.TaskId);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return sent;
    }

    private static EmailMessage BuildMessage(TodoTask task, ApplicationUser user, CultureInfo culture)
    {
        var name = string.IsNullOrWhiteSpace(user.DisplayName)
            ? Localize(culture, "ReminderFallbackName")
            : user.DisplayName;

        var subject = Localize(culture, "ReminderSubject", task.Title);

        var lines = new List<string>
        {
            Localize(culture, "ReminderGreeting", name),
            Localize(culture, "ReminderIntro"),
            task.Title
        };

        if (task.Project is not null)
        {
            lines.Add(Localize(culture, "ReminderProject", task.Project.Name));
        }

        if (FormatDue(task, culture) is { } due)
        {
            lines.Add(Localize(culture, "ReminderDue", due));
        }

        if (!string.IsNullOrWhiteSpace(task.Notes))
        {
            lines.Add(Localize(culture, "ReminderNotes", task.Notes));
        }

        lines.Add(Localize(culture, "ReminderFooter"));

        var html = new StringBuilder();

        foreach (var line in lines)
        {
            html.Append("<p>").Append(WebUtility.HtmlEncode(line)).Append("</p>");
        }

        return new EmailMessage(
            user.Email!,
            subject,
            html.ToString(),
            string.Join(Environment.NewLine, lines));
    }

    private static string Localize(CultureInfo culture, string key, params object?[] arguments)
    {
        var template = Resources.GetString(key, culture) ?? key;

        return arguments.Length == 0
            ? template
            : string.Format(culture, template, arguments);
    }

    private static string? FormatDue(TodoTask task, CultureInfo culture)
    {
        if (task.DueDate is not { } date)
        {
            return null;
        }

        return task.DueTime is { } time
            ? $"{date.ToString("D", culture)} {time.ToString("t", culture)}"
            : date.ToString("D", culture);
    }

    private static CultureInfo ResolveCulture(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return CultureInfo.GetCultureInfo("en");
        }

        try
        {
            return CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("en");
        }
    }

    private static string Truncate(string value)
        => value.Length <= 2000 ? value : value[..2000];
}
