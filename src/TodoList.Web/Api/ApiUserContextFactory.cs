using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using TodoList.Application.Common;
using TodoList.Infrastructure.Identity;

namespace TodoList.Web.Api;

/// <summary>
/// Resolves the bearer-authenticated user into an explicit <see cref="UserContext"/>
/// for API endpoints. API requests never carry an ambient Blazor user.
/// </summary>
public sealed class ApiUserContextFactory(UserManager<ApplicationUser> userManager)
{
    public async Task<UserContext?> GetAsync(ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);

        return user is null ? null : new UserContext(user.Id, user.TimeZoneId);
    }
}
