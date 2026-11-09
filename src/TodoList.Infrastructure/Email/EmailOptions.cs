namespace TodoList.Infrastructure.Email;

/// <summary>
/// SMTP settings bound from the <c>Email</c> configuration section. When
/// <see cref="Host"/> is empty the app falls back to the logging sender.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? Host { get; set; }

    public int Port { get; set; } = 25;

    public bool UseSsl { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "no-reply@todolist.local";

    public string FromName { get; set; } = "TodoList";
}
