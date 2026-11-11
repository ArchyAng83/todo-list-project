using Microsoft.Extensions.Localization;
using TodoList.Application.Common;

namespace TodoList.Web.Services;

/// <summary>
/// Turns an <see cref="Error"/> from the Application layer into a message in the
/// current UI culture. Unknown codes fall back to the English detail so a new
/// error never becomes a blank alert.
/// </summary>
public sealed class ErrorLocalizer(IStringLocalizer<SharedResource> localizer)
{
    public string Describe(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var key = error.Code switch
        {
            ErrorCodes.Validation => "ErrorValidation",
            ErrorCodes.NotFound => "ErrorNotFound",
            ErrorCodes.Conflict => "ErrorConflict",
            ErrorCodes.ConcurrencyConflict => "ErrorConcurrency",
            ErrorCodes.Forbidden => "ErrorForbidden",
            ErrorCodes.Unexpected => "ErrorUnexpected",
            ErrorCodes.InvalidTaskStatus => "ErrorInvalidTaskStatus",
            ErrorCodes.ImportInvalid => "ErrorImportInvalid",
            ValidationCodes.Required => "ErrorRequired",
            ValidationCodes.TooLong => "ErrorTooLong",
            ValidationCodes.InvalidEnum => "ErrorInvalidEnum",
            ValidationCodes.InvalidColor => "ErrorInvalidColor",
            ValidationCodes.OutOfRange => "ErrorOutOfRange",
            ValidationCodes.DueTimeRequiresDueDate => "ErrorDueTimeRequiresDueDate",
            ValidationCodes.ReminderRequiresDueDate => "ErrorReminderRequiresDueDate",
            ValidationCodes.RecurrenceRequiresDueDate => "ErrorRecurrenceRequiresDueDate",
            ValidationCodes.RecurrenceEndBeforeDue => "ErrorRecurrenceEndBeforeDue",
            ValidationCodes.TargetIndexInvalid => "ErrorTargetIndexInvalid",
            ValidationCodes.InvalidCulture => "ErrorInvalidCulture",
            ValidationCodes.InvalidTheme => "ErrorInvalidTheme",
            ValidationCodes.InvalidTimeZone => "ErrorInvalidTimeZone",
            _ => null
        };

        if (key is null)
        {
            return error.Message;
        }

        var localized = localizer[key];

        if (localized.ResourceNotFound)
        {
            return error.Message;
        }

        var arguments = Arguments(key, error);

        return arguments.Length == 0
            ? localized.Value
            : string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                localized.Value,
                arguments);
    }

    private static object?[] Arguments(string key, Error error) => key switch
    {
        "ErrorTooLong" => [Placeholder(error, "MaxLength")],
        "ErrorOutOfRange" => [Placeholder(error, "From"), Placeholder(error, "To")],
        _ => []
    };

    private static object Placeholder(Error error, string name)
        => error.Data is not null && error.Data.TryGetValue(name, out var value) && value is not null
            ? value
            : string.Empty;
}
