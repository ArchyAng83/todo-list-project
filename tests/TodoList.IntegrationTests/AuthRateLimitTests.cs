using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TodoList.Infrastructure.Identity;
using TodoList.Testing;

namespace TodoList.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class AuthRateLimitTests(TodoListAppFixture fixture)
{
    private const string Password = "Password1";
    private const string ClientIpHeader = "X-Test-Client-Ip";

    [Fact]
    public async Task Login_RepeatedAttemptsFromOneIp_ReturnsTooManyRequests()
    {
        var email = await CreateUserAsync();
        await using var factory = new RateLimitedFactory(fixture.ConnectionString, permitLimit: 2);

        using var blockedClient = CreateClient(factory, "203.0.113.10");

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var allowed = await blockedClient.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { email, password = "WrongPass1" });

            allowed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var rejected = await blockedClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = Password });

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.TryGetValues("Retry-After", out var retryAfter).ShouldBeTrue();
        retryAfter.ShouldNotBeNull();

        var retryAfterSeconds = int.Parse(retryAfter!.First(), CultureInfo.InvariantCulture);
        retryAfterSeconds.ShouldBeGreaterThan(0);

        // Another client is a different partition, so a valid login still works.
        using var otherClient = CreateClient(factory, "203.0.113.11");
        var login = await otherClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = Password });

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RateLimit_NonAuthenticationPaths_AreNotThrottled()
    {
        await using var factory = new RateLimitedFactory(fixture.ConnectionString, permitLimit: 1);
        using var client = CreateClient(factory, "203.0.113.12");

        (await client.GetAsync("/health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The single permit is spent on the login POST, yet page loads still work.
        await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "nobody@example.com", password = "WrongPass1" });

        var rejected = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "nobody@example.com", password = "WrongPass1" });

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await client.GetAsync("/health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static HttpClient CreateClient(TodoListWebApplicationFactory factory, string clientIp)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ClientIpHeader, clientIp);
        return client;
    }

    private async Task<string> CreateUserAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var email = $"ratelimit-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            DisplayName = "Rate Limited",
            Culture = "en",
            TimeZoneId = "UTC",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, Password);
        result.Succeeded.ShouldBeTrue(
            string.Join(", ", result.Errors.Select(error => error.Description)));

        return email;
    }

    /// <summary>
    /// A narrow limit, so a single test can trip the limiter without throttling
    /// the rest of the collection.
    /// </summary>
    private sealed class RateLimitedFactory(string connectionString, int permitLimit)
        : TodoListWebApplicationFactory(connectionString, new Dictionary<string, string?>
        {
            ["Auth:RateLimit:PermitLimit"] = permitLimit.ToString(CultureInfo.InvariantCulture),
            ["Auth:RateLimit:WindowSeconds"] = "60"
        })
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            // TestServer leaves RemoteIpAddress unset, so every in-process
            // client would share one partition. Read it from a header instead.
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter, ClientIpStartupFilter>());
        }
    }

    private sealed class ClientIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            => app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    if (context.Request.Headers.TryGetValue(ClientIpHeader, out var value)
                        && IPAddress.TryParse(value, out var address))
                    {
                        context.Connection.RemoteIpAddress = address;
                    }

                    await nextMiddleware(context);
                });

                next(app);
            };
    }
}
