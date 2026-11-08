using Microsoft.AspNetCore.Mvc.Testing;

namespace TodoList.Testing;

public sealed class TodoListAppFixture : IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres = new();

    public TodoListWebApplicationFactory Factory { get; private set; } = null!;

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
