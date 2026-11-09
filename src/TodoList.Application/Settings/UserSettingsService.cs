using FluentValidation;
using TodoList.Application.Common;

namespace TodoList.Application.Settings;

public sealed class UserSettingsService(
    IUserSettingsStore store,
    IValidator<UpdateUserSettingsRequest> validator)
{
    public Task<UserSettings?> GetAsync(UserContext user, CancellationToken cancellationToken = default)
        => store.GetAsync(user.UserId, cancellationToken);

    public async Task<Result<UserSettings>> UpdateAsync(
        UserContext user,
        UpdateUserSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            return Result.Failure<UserSettings>(ValidationErrors.From(validation));
        }

        var settings = new UserSettings(
            request.DisplayName.Trim(),
            request.Culture.ToLowerInvariant(),
            request.TimeZoneId,
            request.Theme.ToLowerInvariant(),
            request.WeekStart,
            request.DefaultReminderMinutes);

        var saved = await store.SaveAsync(user.UserId, settings, cancellationToken);

        return saved
            ? Result.Success(settings)
            : Result.Failure<UserSettings>(Error.NotFound("User was not found."));
    }
}
