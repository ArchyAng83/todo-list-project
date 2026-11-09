using Microsoft.AspNetCore.Identity;
using TodoList.Application.Settings;

namespace TodoList.Infrastructure.Identity;

public sealed class UserSettingsStore(UserManager<ApplicationUser> userManager) : IUserSettingsStore
{
    public async Task<UserSettings?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        return user is null
            ? null
            : new UserSettings(
                user.DisplayName,
                user.Culture,
                user.TimeZoneId,
                user.Theme,
                user.WeekStart,
                user.DefaultReminderMinutes);
    }

    public async Task<bool> SaveAsync(
        Guid userId,
        UserSettings settings,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return false;
        }

        user.DisplayName = settings.DisplayName;
        user.Culture = settings.Culture;
        user.TimeZoneId = settings.TimeZoneId;
        user.Theme = settings.Theme;
        user.WeekStart = settings.WeekStart;
        user.DefaultReminderMinutes = settings.DefaultReminderMinutes;

        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "User settings could not be saved: " + string.Join(", ", result.Errors.Select(error => error.Description)));
        }

        return true;
    }
}
