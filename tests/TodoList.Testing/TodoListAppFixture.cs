using Microsoft.AspNetCore.Mvc.Testing;

namespace TodoList.Testing;

public sealed class TodoListAppFixture : IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres = new();

    public TodoListWebApplicationFactory Factory { get; private set; } = null!;

    /// <summary>
    /// Shared database, exposed so a test can start an extra host with
    /// different configuration without migrating a second database.
    /// </summary>
    public string ConnectionString => _postgres.ConnectionString;

    public HttpClient CreateClient(bool allowAutoRedirect = true)
    {
        return Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = true
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        Factory = new TodoListWebApplicationFactory(_postgres.ConnectionString);

        // Starts the host, which applies migrations.
        using var client = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
