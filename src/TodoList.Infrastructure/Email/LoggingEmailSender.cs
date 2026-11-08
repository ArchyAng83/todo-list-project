using Microsoft.Extensions.Logging;
using TodoList.Application.Email;

namespace TodoList.Infrastructure.Email;

/// <summary>
/// Development email sender that writes messages to the log.
/// Replace with an SMTP sender backed by Mailpit/production SMTP in a later phase.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        logger.LogInformation(
            "Email to {Recipient}: {Subject}\n{Body}",
            message.To,
            message.Subject,
            message.TextBody ?? message.HtmlBody);

        return Task.CompletedTask;
    }
}
