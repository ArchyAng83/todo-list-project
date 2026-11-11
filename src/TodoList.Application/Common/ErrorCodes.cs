namespace TodoList.Application.Common;

/// <summary>
/// Stable error codes shared with the Web layer. The UI maps codes to localized
/// messages, so a code must never be a user-facing string and must stay stable
/// when the English fallback message changes.
/// </summary>
public static class ErrorCodes
{
    public const string Validation = "validation";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string Forbidden = "forbidden";
    public const string Unexpected = "unexpected";
    public const string InvalidTaskStatus = "invalid_task_status";
    public const string ImportInvalid = "import_invalid";
}
