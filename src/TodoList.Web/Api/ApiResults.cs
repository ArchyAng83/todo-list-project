using TodoList.Application.Common;

namespace TodoList.Web.Api;

public static class ApiResults
{
    public static IResult FromError(Error error) => error.Code switch
    {
        "not_found" => Results.NotFound(new ProblemResponse(error.Message)),
        "conflict" => Results.Conflict(new ProblemResponse(error.Message)),
        "validation" => Results.BadRequest(new ProblemResponse(error.Message)),
        "forbidden" => Results.Forbid(),
        _ => Results.Problem(error.Message, statusCode: StatusCodes.Status500InternalServerError)
    };
}
