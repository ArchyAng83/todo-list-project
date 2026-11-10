namespace TodoList.Application.Common;

public sealed record Error(
    string Code,
    string Message,
    IReadOnlyDictionary<string, object?>? Data = null)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Validation(string message) => new(ErrorCodes.Validation, message);

    public static Error Validation(string code, string message) => new(code, message);

    public static Error NotFound(string message) => new(ErrorCodes.NotFound, message);

    public static Error Conflict(string message) => new(ErrorCodes.Conflict, message);

    public static Error Conflict(string code, string message) => new(code, message);

    public static Error Forbidden(string message) => new(ErrorCodes.Forbidden, message);

    public static Error Unexpected(string message) => new(ErrorCodes.Unexpected, message);
}
