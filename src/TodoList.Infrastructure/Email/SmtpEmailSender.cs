using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TodoList.Application.Email;

namespace TodoList.Infrastructure.Email;

/// <summary>
/// Sends transactional email through a configured SMTP server. In development
/// this points at Mailpit, so every message is inspectable at
/// <c>http://localhost:8025</c>.
/// </summary>
public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            throw new InvalidOperationException("The SMTP host is not configured.");
        }

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(settings.UserName))
        {
            client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };

        mail.To.Add(message.To);

        if (!string.IsNullOrWhiteSpace(message.TextBody))
        {
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                message.TextBody,
                null,
                MediaTypeNames.Text.Plain));
        }

        await client.SendMailAsync(mail, cancellationToken);

        logger.LogInformation("Email sent to {Recipient}: {Subject}", message.To, message.Subject);
    }
}
