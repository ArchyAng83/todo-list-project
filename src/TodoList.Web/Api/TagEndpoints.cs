using System.Security.Claims;
using TodoList.Application.Common;
using TodoList.Application.Tags;

namespace TodoList.Web.Api;

public static class TagEndpoints
{
    public static RouteGroupBuilder MapTagEndpoints(this RouteGroupBuilder group)
    {
        var tags = group.MapGroup("/tags").WithTags("Tags");

        tags
            .MapGet("", ListAsync)
            .WithName("ListTags")
            .Produces<IReadOnlyList<TagListItem>>();

        tags
            .MapPost("", CreateAsync)
            .WithName("CreateTag")
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        tags
            .MapPut("/{id:guid}", UpdateAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        tags
            .MapDelete("/{id:guid}", DeleteAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> ListAsync(
        TagQueries queries,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await queries.ListAsync(user, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> CreateAsync(
        TagBody body,
        TagService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.CreateAsync(
            user,
            new CreateTagRequest(body.Name, body.Color),
            cancellationToken);

        return result.IsFailure
            ? ApiResults.FromError(result.Error)
            : Results.Created($"/api/v1/tags/{result.Value}", new CreatedResponse(result.Value));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        TagBody body,
        TagService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.UpdateAsync(
            user,
            id,
            new UpdateTagRequest(body.Name, body.Color),
            cancellationToken);

        return ToNoContent(result);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        TagService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.DeleteAsync(user, id, cancellationToken);

        return ToNoContent(result);
    }

    private static IResult ToNoContent(Result result)
        => result.IsFailure ? ApiResults.FromError(result.Error) : Results.NoContent();
}
