namespace TodoList.Infrastructure.Reminders;

/// <summary>
/// Background reminder delivery settings bound from the <c>Reminders</c>
/// section. Tests disable the dispatcher so email sends are deterministic.
/// </summary>
public sealed class ReminderOptions
{
    public const string SectionName = "Reminders";

    public bool Enabled { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 30;

    public int BatchSize { get; set; } = 25;
}
