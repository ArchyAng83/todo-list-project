using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace TodoList.E2ETests;

/// <summary>
/// A user that a single journey owns. The browser context is registered and
/// signed in, and the API client seeds exactly the data the journey asserts on.
/// Journeys never share a user, so execution order and parallelisation cannot
/// leak state between them.
/// </summary>
public sealed class TestUser : IAsyncDisposable
{
    private const string AccountPassword = "E2ePass123";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private TestUser(IBrowserContext context, HttpClient api, string email)
    {
        Context = context;
        Api = api;
        Email = email;
    }

    public IBrowserContext Context { get; }

    /// <summary>Bearer-authenticated client for seeding data through the real API.</summary>
    public HttpClient Api { get; }

    public string Email { get; }

    public string Password => AccountPassword;

    public static async Task<TestUser> RegisterAsync(E2EFixture fixture, IBrowserContext context)
    {
        var email = $"e2e-{Guid.NewGuid():N}@example.com";
        var page = await context.NewPageAsync();

        await page.GotoAsync("/account/register");
        await page.Locator("#display-name").FillAsync("E2E User");
        await page.Locator("#email").FillAsync(email);
        await page.Locator("#password").FillAsync(AccountPassword);
        await page.Locator("#confirm-password").FillAsync(AccountPassword);
        await page.Locator("button[type=submit]").ClickAsync();

        // Registration signs the user in and redirects to the task list.
        await Expect(page).ToHaveURLAsync(new Regex("/tasks$"), new() { Timeout = 30_000 });
        await Pages.WaitForBlazorAsync(page);
        await page.CloseAsync();

        var token = await LoginAsync(fixture.BaseUrl, email);
        var api = new HttpClient { BaseAddress = new Uri(fixture.BaseUrl) };
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return new TestUser(context, api, email);
    }

    public async Task AddTaskAsync(
        string title,
        DateOnly? dueDate = null,
        string recurrence = "None",
        TimeOnly? dueTime = null)
    {
        var response = await Api.PostAsJsonAsync("/api/v1/tasks", new
        {
            title,
            dueDate,
            dueTime,
            recurrence
        });

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Creating task '{title}' failed with {(int)response.StatusCode}: "
                + await response.Content.ReadAsStringAsync());
        }
    }

    public async ValueTask DisposeAsync()
    {
        Api.Dispose();
        await Context.DisposeAsync();
    }

    private static async Task<string> LoginAsync(string baseUrl, string email)
    {
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = AccountPassword });

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"API login for '{email}' failed with {(int)response.StatusCode}: "
                + await response.Content.ReadAsStringAsync());
        }

        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        return tokens?.AccessToken
            ?? throw new InvalidOperationException("The login response did not contain an access token.");
    }

    private sealed record TokenResponse(
        string TokenType,
        string AccessToken,
        long ExpiresIn,
        string RefreshToken);
}
