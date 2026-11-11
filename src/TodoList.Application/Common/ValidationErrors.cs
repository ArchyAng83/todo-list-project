using FluentValidation.Results;

namespace TodoList.Application.Common;

public static class ValidationErrors
{
    /// <summary>
    /// Aggregates the failure messages into an <see cref="Error"/>. The first
    /// failure's stable code is used when every validator declared one, so the
    /// UI can localize the message; otherwise the generic validation code wins
    /// and the English detail stays as a fallback.
    /// </summary>
    public static Error From(ValidationResult result)
    {
        var failures = result.Errors;

        var messages = failures
            .Select(error => error.ErrorMessage)
            .Distinct(StringComparer.Ordinal);

        var message = string.Join("; ", messages);
        var first = failures.FirstOrDefault();

        if (first is null || !ValidationCodes.IsValidation(first.ErrorCode))
        {
            return Error.Validation(message);
        }

        var data = first.FormattedMessagePlaceholderValues is { Count: > 0 } values
            ? values.ToDictionary(pair => pair.Key, pair => (object?)pair.Value)
            : null;

        return new Error(first.ErrorCode, message, data);
    }
}
