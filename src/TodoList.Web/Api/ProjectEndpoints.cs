using System.Security.Claims;
using TodoList.Application.Common;
using TodoList.Application.Projects;

namespace TodoList.Web.Api;

public static class ProjectEndpoints
{
    public static RouteGroupBuilder MapProjectEndpoints(this RouteGroupBuilder group)
    {
        var projects = group.MapGroup("/projects").WithTags("Projects");

        projects
            .MapGet("", ListAsync)
            .WithName("ListProjects")
            .Produces<IReadOnlyList<ProjectListItem>>();

        projects
            .MapPost("", CreateAsync)
            .WithName("CreateProject")
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        projects
            .MapPut("/{id:guid}", UpdateAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects
            .MapPost("/{id:guid}/archive", SetArchivedAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        projects
            .MapDelete("/{id:guid}", DeleteAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> ListAsync(
        ProjectQueries queries,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        bool? includeArchived,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await queries.ListAsync(user, includeArchived ?? false, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> CreateAsync(
        ProjectBody body,
        ProjectService service,
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
            new CreateProjectRequest(body.Name, body.Color),
            cancellationToken);

        return result.IsFailure
            ? ApiResults.FromError(result.Error)
            : Results.Created($"/api/v1/projects/{result.Value}", new CreatedResponse(result.Value));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        ProjectBody body,
        ProjectService service,
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
            new UpdateProjectRequest(body.Name, body.Color),
            cancellationToken);

        return ToNoContent(result);
    }

    private static async Task<IResult> SetArchivedAsync(
        Guid id,
        ArchiveProjectBody body,
        ProjectService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.SetArchivedAsync(user, id, body.IsArchived, cancellationToken);

        return ToNoContent(result);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        ProjectService service,
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
