namespace TodoList.Application.Common;

/// <summary>
/// Stable FluentValidation error codes. Validators attach them with
/// <c>WithErrorCode</c> so the Web layer can map a failure to a localized
/// message instead of showing the English fallback text.
/// </summary>
public static class ValidationCodes
{
    public const string Required = "required";
    public const string TooLong = "too_long";
    public const string InvalidEnum = "invalid_enum";
    public const string InvalidColor = "invalid_color";
    public const string OutOfRange = "out_of_range";
    public const string DueTimeRequiresDueDate = "due_time_requires_due_date";
    public const string ReminderRequiresDueDate = "reminder_requires_due_date";
    public const string RecurrenceRequiresDueDate = "recurrence_requires_due_date";
    public const string RecurrenceEndBeforeDue = "recurrence_end_before_due";
    public const string TargetIndexInvalid = "target_index_invalid";
    public const string InvalidCulture = "invalid_culture";
    public const string InvalidTheme = "invalid_theme";
    public const string InvalidTimeZone = "invalid_time_zone";
}
