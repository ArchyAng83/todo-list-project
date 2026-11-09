namespace TodoList.Application.Settings;

public sealed record UserSettings(
    string DisplayName,
    string Culture,
    string TimeZoneId,
    string Theme,
    DayOfWeek WeekStart,
    int DefaultReminderMinutes);
