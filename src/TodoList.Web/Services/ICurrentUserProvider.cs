using TodoList.Application.Common;

namespace TodoList.Web.Services;

/// <summary>
/// Resolves the authenticated user into an explicit <see cref="UserContext"/>.
/// Abstracted so components can be tested without the Identity stack.
/// </summary>
public interface ICurrentUserProvider
{
    Task<UserContext?> GetAsync();
}
