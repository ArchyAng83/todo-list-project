using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TodoList.Application.Common;
using TodoList.Infrastructure.Identity;

namespace TodoList.Web.Services;

/// <summary>
/// Resolves the authenticated user into an explicit <see cref="UserContext"/> for
/// Application services. Components never pass an ambient user to the domain.
/// </summary>
/// <remarks>
/// Each call runs in its own scope. Blazor Server keeps one service scope per
/// circuit, and layout and page components load concurrently, so sharing the
/// circuit-scoped <see cref="UserManager{TUser}"/> (and its
/// <c>DbContext</c>) would start overlapping database operations.
/// </remarks>
public sealed class CurrentUserProvider(
    AuthenticationStateProvider authenticationStateProvider,
    IServiceScopeFactory scopeFactory) : ICurrentUserProvider
{
    public async Task<UserContext?> GetAsync()
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();

        if (state.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.GetUserAsync(state.User);

        return user is null ? null : new UserContext(user.Id, user.TimeZoneId);
    }
}
