using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TodoList.Testing;

public sealed class TodoListWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development enables automatic migration at startup.
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Seed:Demo", "false");
    }
}
