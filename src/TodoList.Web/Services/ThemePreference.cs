namespace TodoList.Web.Services;

/// <summary>
/// Holds the active theme preference for the current circuit so the layout and
/// the settings page stay in sync without a full page reload.
/// </summary>
public sealed class ThemePreference
{
    public string Theme { get; private set; } = "system";

    public event Action? Changed;

    public void Set(string theme)
    {
        var normalized = string.IsNullOrWhiteSpace(theme) ? "system" : theme.ToLowerInvariant();

        if (Theme == normalized)
        {
            return;
        }

        Theme = normalized;
        Changed?.Invoke();
    }
}
