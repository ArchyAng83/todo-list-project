using System.Security.Claims;
using TodoList.Application.Settings;

namespace TodoList.Web.Api;

public static class MeEndpoints
{
    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder group)
    {
        var me = group.MapGroup("/me").WithTags("Me");

        me
            .MapGet("/settings", GetSettingsAsync)
            .WithName("GetSettings")
            .Produces<UserSettings>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        me
            .MapPut("/settings", UpdateSettingsAsync)
            .Produces<UserSettings>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetSettingsAsync(
        UserSettingsService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var settings = await service.GetAsync(user, cancellationToken);

        return settings is null ? Results.NotFound() : Results.Ok(settings);
    }

    private static async Task<IResult> UpdateSettingsAsync(
        UpdateUserSettingsRequest request,
        UserSettingsService service,
        ApiUserContextFactory users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.UpdateAsync(user, request, cancellationToken);

        return result.IsFailure ? ApiResults.FromError(result.Error) : Results.Ok(result.Value);
    }
}
