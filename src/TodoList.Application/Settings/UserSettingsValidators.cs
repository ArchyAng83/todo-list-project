using FluentValidation;
using TodoList.Application.Common;

namespace TodoList.Application.Settings;

public sealed class UpdateUserSettingsRequestValidator : AbstractValidator<UpdateUserSettingsRequest>
{
    public UpdateUserSettingsRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithErrorCode(ValidationCodes.Required)
            .MaximumLength(UserSettingsConstraints.DisplayNameMaxLength)
            .WithErrorCode(ValidationCodes.TooLong);

        RuleFor(x => x.Culture)
            .Must(culture => UserSettingsConstraints.Cultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
            .WithErrorCode(ValidationCodes.InvalidCulture)
            .WithMessage("Culture must be one of: en, ru.");

        RuleFor(x => x.Theme)
            .Must(theme => UserSettingsConstraints.Themes.Contains(theme, StringComparer.OrdinalIgnoreCase))
            .WithErrorCode(ValidationCodes.InvalidTheme)
            .WithMessage("Theme must be one of: system, light, dark.");

        RuleFor(x => x.TimeZoneId)
            .Must(BeValidTimeZone)
            .WithErrorCode(ValidationCodes.InvalidTimeZone)
            .WithMessage("Time zone is not recognized.");

        RuleFor(x => x.WeekStart)
            .IsInEnum()
            .WithErrorCode(ValidationCodes.InvalidEnum);

        RuleFor(x => x.DefaultReminderMinutes)
            .InclusiveBetween(
                UserSettingsConstraints.DefaultReminderMinMinutes,
                UserSettingsConstraints.DefaultReminderMaxMinutes)
            .WithErrorCode(ValidationCodes.OutOfRange);
    }

    private static bool BeValidTimeZone(string timeZoneId)
        => !string.IsNullOrWhiteSpace(timeZoneId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);
}
