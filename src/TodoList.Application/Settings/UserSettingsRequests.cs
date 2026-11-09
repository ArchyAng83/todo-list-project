namespace TodoList.Application.Settings;

public sealed record UpdateUserSettingsRequest(
    string DisplayName,
    string Culture,
    string TimeZoneId,
    string Theme,
    DayOfWeek WeekStart,
    int DefaultReminderMinutes);
