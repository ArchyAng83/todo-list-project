using System.Security.Claims;
using System.Text;
using System.Text.Json;
using TodoList.Application.DataTransfer;
using TodoList.Web.Services;

namespace TodoList.Web.Api;

public static class DataEndpoints
{
    public static RouteGroupBuilder MapDataEndpoints(this RouteGroupBuilder group)
    {
        var data = group.MapGroup("/data").WithTags("Data");

        data
            .MapGet("/export", ExportJsonAsync)
            .WithName("ExportData")
            .Produces<ExportDocument>();

        data
            .MapGet("/export/csv", ExportCsvAsync)
            .WithName("ExportDataCsv")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv");

        data
            .MapPost("/import", ImportAsync)
            .WithName("ImportData")
            .Produces<ImportSummary>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    private static async Task<IResult> ExportJsonAsync(
        DataTransferService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var document = await service.ExportAsync(user, cancellationToken);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, DataTransferJson.Options);
        var fileName = $"todolist-export-{timeProvider.GetUtcNow():yyyyMMdd}.json";

        return Results.File(bytes, "application/json; charset=utf-8", fileName);
    }

    private static async Task<IResult> ExportCsvAsync(
        DataTransferService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var csv = await service.ExportTasksCsvAsync(user, cancellationToken);
        var bytes = Encoding.UTF8.GetBytes(csv);
        var fileName = $"{TaskCsvExport.FileNamePrefix}-{timeProvider.GetUtcNow():yyyyMMdd}.csv";

        return Results.File(bytes, "text/csv; charset=utf-8", fileName);
    }

    private static async Task<IResult> ImportAsync(
        ExportDocument? document,
        DataTransferService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (document is null)
        {
            return Results.BadRequest(new ProblemResponse("An import document is required."));
        }

        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.ImportAsync(user, document, cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : ApiResults.FromError(result.Error);
    }
}
