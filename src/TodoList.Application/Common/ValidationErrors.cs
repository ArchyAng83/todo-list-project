using FluentValidation.Results;

namespace TodoList.Application.Common;

public static class ValidationErrors
{
    public static Error From(ValidationResult result)
    {
        var messages = result.Errors
            .Select(error => error.ErrorMessage)
            .Distinct(StringComparer.Ordinal);

        return Error.Validation(string.Join("; ", messages));
    }
}
