using TodoList.Domain.Tasks;

namespace TodoList.Application.Settings;

public static class UserSettingsConstraints
{
    public const int DisplayNameMaxLength = 100;

    public const int DefaultReminderMinMinutes = 0;

    public const int DefaultReminderMaxMinutes = TodoTask.MaxReminderOffsetMinutes;

    public static readonly string[] Cultures = ["en", "ru"];

    public static readonly string[] Themes = ["system", "light", "dark"];
}
