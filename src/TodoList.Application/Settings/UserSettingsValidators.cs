using FluentValidation;

namespace TodoList.Application.Settings;

public sealed class UpdateUserSettingsRequestValidator : AbstractValidator<UpdateUserSettingsRequest>
{
    public UpdateUserSettingsRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(UserSettingsConstraints.DisplayNameMaxLength);

        RuleFor(x => x.Culture)
            .Must(culture => UserSettingsConstraints.Cultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Culture must be one of: en, ru.");

        RuleFor(x => x.Theme)
            .Must(theme => UserSettingsConstraints.Themes.Contains(theme, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Theme must be one of: system, light, dark.");

        RuleFor(x => x.TimeZoneId)
            .Must(BeValidTimeZone)
            .WithMessage("Time zone is not recognized.");

        RuleFor(x => x.WeekStart)
            .IsInEnum();

        RuleFor(x => x.DefaultReminderMinutes)
            .InclusiveBetween(
                UserSettingsConstraints.DefaultReminderMinMinutes,
                UserSettingsConstraints.DefaultReminderMaxMinutes);
    }

    private static bool BeValidTimeZone(string timeZoneId)
        => !string.IsNullOrWhiteSpace(timeZoneId)
            && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);
}
