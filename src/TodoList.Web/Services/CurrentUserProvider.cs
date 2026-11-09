using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using TodoList.Application.Common;
using TodoList.Infrastructure.Identity;

namespace TodoList.Web.Services;

/// <summary>
/// Resolves the current user into an explicit <see cref="UserContext"/> for
/// Application services. Components never pass an ambient user to the domain.
/// </summary>
public sealed class CurrentUserProvider(
    AuthenticationStateProvider authenticationStateProvider,
    UserManager<ApplicationUser> userManager)
{
    public async Task<UserContext?> GetAsync()
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();

        if (state.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var user = await userManager.GetUserAsync(state.User);

        return user is null ? null : new UserContext(user.Id, user.TimeZoneId);
    }
}
