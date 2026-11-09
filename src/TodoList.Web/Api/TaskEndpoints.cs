using System.Security.Claims;
using TodoList.Application.Common;
using TodoList.Application.Tasks;
using TodoList.Domain.Tasks;

namespace TodoList.Web.Api;

public static class TaskEndpoints
{
    public static RouteGroupBuilder MapTaskEndpoints(this RouteGroupBuilder group)
    {
        var tasks = group.MapGroup("/tasks").WithTags("Tasks");

        tasks
            .MapGet("", ListAsync)
            .WithName("ListTasks")
            .Produces<IReadOnlyList<TaskListItem>>();

        tasks
            .MapGet("/board", GetBoardAsync)
            .WithName("GetTaskBoard")
            .Produces<TaskBoard>();

        tasks
            .MapGet("/{id:guid}", GetAsync)
            .WithName("GetTask")
            .Produces<TaskDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        tasks
            .MapPost("", CreateAsync)
            .WithName("CreateTask")
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        tasks
            .MapPut("/{id:guid}", UpdateAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        tasks
            .MapPost("/{id:guid}/status", SetStatusAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        tasks
            .MapPost("/{id:guid}/move", MoveAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        tasks
            .MapDelete("/{id:guid}", DeleteAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        tasks
            .MapPost("/{id:guid}/checklist-items", AddChecklistItemAsync)
            .Produces<CreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        tasks
            .MapPost("/{id:guid}/checklist-items/{itemId:guid}/completed", SetChecklistItemCompletedAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        tasks
            .MapDelete("/{id:guid}/checklist-items/{itemId:guid}", RemoveChecklistItemAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        tasks
            .MapPost("/{id:guid}/tags", AddTagAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        tasks
            .MapDelete("/{id:guid}/tags/{tagId:guid}", RemoveTagAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> ListAsync(
        TaskQueries queries,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        TaskView? view,
        TodoTaskStatus? status,
        Guid? projectId,
        Guid? tagId,
        string? search,
        TaskSort? sort,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var filter = new TaskListFilter(
            status,
            projectId,
            tagId,
            search,
            view ?? TaskView.All,
            sort ?? TaskSort.Default);

        var result = await queries.ListAsync(user, filter, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetBoardAsync(
        TaskQueries queries,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var board = await queries.BoardAsync(user, cancellationToken);

        return Results.Ok(board);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        TaskQueries queries,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var task = await queries.GetAsync(user, id, cancellationToken);

        return task is null ? Results.NotFound() : Results.Ok(task);
    }

    private static async Task<IResult> CreateAsync(
        CreateTaskBody body,
        TaskService service,
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
            new CreateTaskRequest(body.Title, body.ProjectId),
            cancellationToken);

        return result.IsFailure
            ? ApiResults.FromError(result.Error)
            : Results.Created($"/api/v1/tasks/{result.Value}", new CreatedResponse(result.Value));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateTaskBody body,
        TaskService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var request = new UpdateTaskRequest(
            body.Title,
            body.Notes,
            body.ProjectId,
            body.Priority,
            body.DueDate,
            body.DueTime,
            body.ReminderOffsetMinutes);

        var result = await service.UpdateAsync(user, id, request, cancellationToken);

        return ToNoContent(result);
    }

    private static async Task<IResult> SetStatusAsync(
        Guid id,
        SetTaskStatusBody body,
        TaskService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.SetStatusAsync(user, id, body.Status, cancellationToken);

        return ToNoContent(result);
    }

    private static async Task<IResult> MoveAsync(
        Guid id,
        MoveTaskBody body,
        TaskService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.MoveAsync(
            user,
            id,
            new MoveTaskRequest(body.Status, body.TargetIndex),
            cancellationToken);

        return ToNoContent(result);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        TaskService service,
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

    private static async Task<IResult> AddChecklistItemAsync(
        Guid id,
        AddChecklistItemBody body,
        TaskService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.AddChecklistItemAsync(
            user,
            id,
            new AddChecklistItemRequest(body.Title),
            cancellationToken);

        return result.IsFailure
            ? ApiResults.FromError(result.Error)
            : Results.Created(
                $"/api/v1/tasks/{id}/checklist-items/{result.Value}",
                new CreatedResponse(result.Value));
    }

    private static async Task<IResult> SetChecklistItemCompletedAsync(
        Guid id,
        Guid itemId,
        SetChecklistItemCompletedBody body,
        TaskService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.SetChecklistItemCompletedAsync(
            user,
            id,
            itemId,
            new SetChecklistItemCompletedRequest(body.IsCompleted),
            cancellationToken);

        return ToNoContent(result);
    }

    private static async Task<IResult> RemoveChecklistItemAsync(
        Guid id,
        Guid itemId,
        TaskService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.RemoveChecklistItemAsync(user, id, itemId, cancellationToken);

        return ToNoContent(result);
    }

    private static async Task<IResult> AddTagAsync(
        Guid id,
        AddTagToTaskBody body,
        TaskService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.AddTagAsync(
            user,
            id,
            new AddTaskTagRequest(body.TagId),
            cancellationToken);

        return ToNoContent(result);
    }

    private static async Task<IResult> RemoveTagAsync(
        Guid id,
        Guid tagId,
        TaskService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.RemoveTagAsync(user, id, tagId, cancellationToken);

        return ToNoContent(result);
    }

    private static IResult ToNoContent(Result result)
        => result.IsFailure ? ApiResults.FromError(result.Error) : Results.NoContent();
}
