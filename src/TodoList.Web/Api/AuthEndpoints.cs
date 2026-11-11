using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TodoList.Infrastructure.Identity;

namespace TodoList.Web.Api;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/v1/auth")
            .WithTags("Auth")
            .AllowAnonymous();

        group
            .MapPost("/login", LoginAsync)
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked);

        group
            .MapPost("/refresh", RefreshAsync)
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginBody body,
        SignInManager<ApplicationUser> signInManager)
    {
        signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;

        var result = await signInManager.PasswordSignInAsync(
            body.Email,
            body.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return Results.Problem("Account is locked out.", statusCode: StatusCodes.Status423Locked);
        }

        if (result.RequiresTwoFactor)
        {
            return Results.Problem(
                "Two-factor authentication is not supported.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!result.Succeeded)
        {
            return Results.Problem("Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);
        }

        // The bearer handler writes the access and refresh tokens as the response body.
        return Results.Empty;
    }

    private static async Task<IResult> RefreshAsync(
        RefreshBody body,
        IOptionsMonitor<BearerTokenOptions> optionsMonitor,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        TimeProvider timeProvider)
    {
        var options = optionsMonitor.Get(IdentityConstants.BearerScheme);
        var ticket = options.RefreshTokenProtector.Unprotect(body.RefreshToken);

        if (ticket?.Principal is null
            || ticket.Properties.ExpiresUtc is not { } expiresUtc
            || timeProvider.GetUtcNow() >= expiresUtc)
        {
            return Unauthorized();
        }

        var user = await userManager.GetUserAsync(ticket.Principal);

        if (user is null || await signInManager.ValidateSecurityStampAsync(ticket.Principal) is null)
        {
            return Unauthorized();
        }

        signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;
        await signInManager.SignInAsync(user, isPersistent: false);

        return Results.Empty;
    }

    private static IResult Unauthorized()
        => Results.Problem(
            "Invalid or expired refresh token.",
            statusCode: StatusCodes.Status401Unauthorized);
}
