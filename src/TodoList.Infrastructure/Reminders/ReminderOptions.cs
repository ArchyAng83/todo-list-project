using System.ComponentModel.DataAnnotations;

namespace TodoList.Infrastructure.Reminders;

/// <summary>
/// Background reminder delivery settings bound from the <c>Reminders</c>
/// section. Tests disable the dispatcher so email sends are deterministic.
/// </summary>
public sealed class ReminderOptions
{
    public const string SectionName = "Reminders";

    public bool Enabled { get; set; } = true;

    [Range(5, 3600)]
    public int PollIntervalSeconds { get; set; } = 30;

    [Range(1, 500)]
    public int BatchSize { get; set; } = 25;

    /// <summary>
    /// Terminal reminders older than this many days are deleted by the
    /// retention worker. Pending reminders are never removed.
    /// </summary>
    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 30;

    [Range(1, 168)]
    public int CleanupIntervalHours { get; set; } = 24;
}
