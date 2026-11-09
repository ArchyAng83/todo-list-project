namespace TodoList.Application.Settings;

/// <summary>
/// Reads and writes the persisted settings of an Identity user. Implemented in
/// Infrastructure so Application does not depend on ASP.NET Core Identity.
/// </summary>
public interface IUserSettingsStore
{
    Task<UserSettings?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> SaveAsync(Guid userId, UserSettings settings, CancellationToken cancellationToken = default);
}
