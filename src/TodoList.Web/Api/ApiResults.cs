using TodoList.Application.Common;

namespace TodoList.Web.Api;

public static class ApiResults
{
    public static IResult FromError(Error error) => error.Code switch
    {
        ErrorCodes.NotFound => Results.NotFound(new ProblemResponse(error.Message)),
        ErrorCodes.Conflict or ErrorCodes.ConcurrencyConflict =>
            Results.Conflict(new ProblemResponse(error.Message)),
        ErrorCodes.Forbidden => Results.Forbid(),
        _ when IsValidation(error.Code) =>
            Results.BadRequest(new ProblemResponse(error.Message)),
        _ => Results.Problem(error.Message, statusCode: StatusCodes.Status500InternalServerError)
    };

    private static bool IsValidation(string code)
        => code is ErrorCodes.Validation or ErrorCodes.InvalidTaskStatus or ErrorCodes.ImportInvalid
            || ValidationCodes.IsValidation(code);
}
