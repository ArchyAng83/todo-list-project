using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TodoList.Testing;

/// <summary>
/// Hosts the web application against a real PostgreSQL database.
/// </summary>
/// <param name="connectionString">Database the host migrates and uses.</param>
/// <param name="settings">
/// Configuration overrides applied on top of the defaults. Keys use the
/// <c>Section:Key</c> form.
/// </param>
public class TodoListWebApplicationFactory(
    string connectionString,
    IReadOnlyDictionary<string, string?>? settings = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development enables automatic migration at startup.
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Seed:Demo", "false");
        builder.UseSetting("Reminders:Enabled", "false");

        // Every in-process test client looks the same to the limiter, so the
        // production auth limit would throttle the suite. Tests that exercise
        // throttling narrow it through <paramref name="settings"/>.
        builder.UseSetting("Auth:RateLimit:PermitLimit", "1000");

        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }
    }
}
