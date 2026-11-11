using Microsoft.AspNetCore.Identity;

namespace TodoList.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public string Culture { get; set; } = "en";

    public string TimeZoneId { get; set; } = "UTC";

    public string Theme { get; set; } = "system";

    public DayOfWeek WeekStart { get; set; } = DayOfWeek.Monday;

    public int DefaultReminderMinutes { get; set; } = 15;

    public DateTimeOffset CreatedAt { get; set; }
}
