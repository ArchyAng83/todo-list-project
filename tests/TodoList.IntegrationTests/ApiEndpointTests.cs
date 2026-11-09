using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TodoList.Application.Tasks;
using TodoList.Domain.Tasks;
using TodoList.Infrastructure.Identity;
using TodoList.Testing;

namespace TodoList.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class ApiEndpointTests(TodoListAppFixture fixture)
{
    private const string Password = "Password1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Login_ValidCredentials_ReturnsBearerTokens()
    {
        var (_, email, _) = await CreateUserAsync();
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = Password });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);

        tokens.ShouldNotBeNull();
        tokens.TokenType.ShouldBe("Bearer");
        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        tokens.ExpiresIn.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        var (_, email, _) = await CreateUserAsync();
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = "WrongPass1" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ValidToken_IssuesNewAccessToken()
    {
        var (_, email, _) = await CreateUserAsync();
        using var client = fixture.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = Password });

        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        tokens.ShouldNotBeNull();

        var refresh = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken });

        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        var refreshed = await refresh.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        refreshed.ShouldNotBeNull();
        refreshed.TokenType.ShouldBe("Bearer");
        refreshed.AccessToken.ShouldNotBeNullOrWhiteSpace();

        using var api = fixture.CreateClient();
        api.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);

        (await api.GetAsync("/api/v1/me/settings")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_InvalidToken_ReturnsUnauthorized()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = "not-a-token" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Tasks_AnonymousRequest_ReturnsUnauthorized()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/tasks");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Tasks_CookieAuthenticationOnly_ReturnsUnauthorized()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);
        var email = $"cookie-{Guid.NewGuid():N}@example.com";

        var registerPage = await client.GetStringAsync("/account/register");
        var token = ExtractAntiforgeryToken(registerPage);

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "register",
            ["__RequestVerificationToken"] = token,
            ["Input.DisplayName"] = "Cookie User",
            ["Input.Email"] = email,
            ["Input.Password"] = Password,
            ["Input.ConfirmPassword"] = Password
        });

        var register = await client.PostAsync("/account/register", form);
        register.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        // The browser cookie must not grant access to the bearer-only API.
        var response = await client.GetAsync("/api/v1/tasks");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Tasks_BearerToken_ScopesToOwner()
    {
        var (_, ownerEmail, _) = await CreateUserAsync();
        var (_, otherEmail, _) = await CreateUserAsync();

        using var ownerClient = await CreateBearerClientAsync(ownerEmail);
        using var otherClient = await CreateBearerClientAsync(otherEmail);

        var create = await ownerClient.PostAsJsonAsync("/api/v1/tasks", new { title = "Secret" });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await create.Content.ReadFromJsonAsync<CreatedResponse>(JsonOptions);
        created.ShouldNotBeNull();
        created.Id.ShouldNotBe(Guid.Empty);

        var ownerTasks = await ownerClient.GetFromJsonAsync<List<TaskListItem>>("/api/v1/tasks", JsonOptions);
        ownerTasks.ShouldNotBeNull();
        ownerTasks.ShouldHaveSingleItem().Title.ShouldBe("Secret");

        var otherTasks = await otherClient.GetFromJsonAsync<List<TaskListItem>>("/api/v1/tasks", JsonOptions);
        otherTasks.ShouldNotBeNull();
        otherTasks.ShouldBeEmpty();

        (await otherClient.GetAsync($"/api/v1/tasks/{created.Id}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);

        (await otherClient.DeleteAsync($"/api/v1/tasks/{created.Id}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);

        (await otherClient.PutAsJsonAsync(
            $"/api/v1/tasks/{created.Id}",
            new
            {
                title = "Hijacked",
                notes = (string?)null,
                projectId = (Guid?)null,
                priority = "High",
                dueDate = (string?)null,
                dueTime = (string?)null,
                reminderOffsetMinutes = (int?)null
            })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MoveTask_BetweenColumns_UpdatesBoardOrder()
    {
        using var client = await CreateBearerClientAsync((await CreateUserAsync()).Email);

        var first = await CreateTaskAsync(client, "First", null, null);
        var second = await CreateTaskAsync(client, "Second", null, null);

        (await client.PostAsJsonAsync(
            $"/api/v1/tasks/{second}/move",
            new { status = "InProgress", targetIndex = 0 }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.PostAsJsonAsync(
            $"/api/v1/tasks/{second}/move",
            new { status = "Todo", targetIndex = 0 }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var board = await client.GetFromJsonAsync<TaskBoard>("/api/v1/tasks/board", JsonOptions);

        board.ShouldNotBeNull();
        var todo = board.Columns.Single(column => column.Status == TodoTaskStatus.Todo);
        todo.Tasks.Select(task => task.Id).ShouldBe([second, first]);
    }

    [Fact]
    public async Task MoveTask_CrossUser_ReturnsNotFound()
    {
        using var ownerClient = await CreateBearerClientAsync((await CreateUserAsync()).Email);
        using var intruderClient = await CreateBearerClientAsync((await CreateUserAsync()).Email);

        var taskId = await CreateTaskAsync(ownerClient, "Private", null, null);

        var response = await intruderClient.PostAsJsonAsync(
            $"/api/v1/tasks/{taskId}/move",
            new { status = "Done", targetIndex = 0 });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TaskViews_FilterByDueDateAndStatus()
    {
        using var client = await CreateBearerClientAsync((await CreateUserAsync()).Email);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var projectId = await CreateProjectAsync(client, "Views project");

        var todayTask = await CreateTaskAsync(client, "Today task", today, projectId);
        var upcomingTask = await CreateTaskAsync(client, "Upcoming task", today.AddDays(3), projectId);
        var inboxTask = await CreateTaskAsync(client, "Inbox task", null, null);
        var completedTask = await CreateTaskAsync(client, "Done task", null, null);

        (await client.PostAsJsonAsync(
            $"/api/v1/tasks/{completedTask}/status",
            new { status = "Done" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var todayView = await client.GetFromJsonAsync<List<TaskListItem>>(
            "/api/v1/tasks?view=Today",
            JsonOptions);
        todayView.ShouldNotBeNull();
        todayView.ShouldHaveSingleItem().Id.ShouldBe(todayTask);

        var upcomingView = await client.GetFromJsonAsync<List<TaskListItem>>(
            "/api/v1/tasks?view=Upcoming",
            JsonOptions);
        upcomingView.ShouldNotBeNull();
        upcomingView.ShouldHaveSingleItem().Id.ShouldBe(upcomingTask);

        var inboxView = await client.GetFromJsonAsync<List<TaskListItem>>(
            "/api/v1/tasks?view=Inbox",
            JsonOptions);
        inboxView.ShouldNotBeNull();
        inboxView.ShouldHaveSingleItem().Id.ShouldBe(inboxTask);

        var completedView = await client.GetFromJsonAsync<List<TaskListItem>>(
            "/api/v1/tasks?view=Completed",
            JsonOptions);
        completedView.ShouldNotBeNull();
        completedView.ShouldHaveSingleItem().Id.ShouldBe(completedTask);
    }

    [Fact]
    public async Task TaskSort_Priority_OrdersUrgentFirst()
    {
        using var client = await CreateBearerClientAsync((await CreateUserAsync()).Email);

        var low = await CreateTaskAsync(client, "Low priority", null, null);
        var urgent = await CreateTaskAsync(client, "Urgent priority", null, null);

        (await client.PutAsJsonAsync(
            $"/api/v1/tasks/{low}",
            new
            {
                title = "Low priority",
                notes = (string?)null,
                projectId = (Guid?)null,
                priority = "Low",
                dueDate = (string?)null,
                dueTime = (string?)null,
                reminderOffsetMinutes = (int?)null
            })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.PutAsJsonAsync(
            $"/api/v1/tasks/{urgent}",
            new
            {
                title = "Urgent priority",
                notes = (string?)null,
                projectId = (Guid?)null,
                priority = "Urgent",
                dueDate = (string?)null,
                dueTime = (string?)null,
                reminderOffsetMinutes = (int?)null
            })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var sorted = await client.GetFromJsonAsync<List<TaskListItem>>(
            "/api/v1/tasks?sort=Priority",
            JsonOptions);

        sorted.ShouldNotBeNull();
        sorted[0].Id.ShouldBe(urgent);
        sorted[0].Priority.ShouldBe(TaskPriority.Urgent);
    }

    [Fact]
    public async Task Settings_UpdateAndGet_RoundTripsAndPersists()
    {
        var (userId, email, _) = await CreateUserAsync();
        using var client = await CreateBearerClientAsync(email);

        var response = await client.PutAsJsonAsync(
            "/api/v1/me/settings",
            new
            {
                displayName = "Renamed User",
                culture = "ru",
                timeZoneId = "UTC",
                theme = "dark",
                weekStart = "Sunday",
                defaultReminderMinutes = 45
            });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await client.GetFromJsonAsync<TodoList.Application.Settings.UserSettings>(
            "/api/v1/me/settings",
            JsonOptions);

        updated.ShouldNotBeNull();
        updated.DisplayName.ShouldBe("Renamed User");
        updated.Culture.ShouldBe("ru");
        updated.Theme.ShouldBe("dark");
        updated.WeekStart.ShouldBe(DayOfWeek.Sunday);
        updated.DefaultReminderMinutes.ShouldBe(45);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());

        user.ShouldNotBeNull();
        user.DisplayName.ShouldBe("Renamed User");
        user.Theme.ShouldBe("dark");
        user.WeekStart.ShouldBe(DayOfWeek.Sunday);
    }

    [Fact]
    public async Task Settings_InvalidTimeZone_ReturnsBadRequest()
    {
        using var client = await CreateBearerClientAsync((await CreateUserAsync()).Email);

        var response = await client.PutAsJsonAsync(
            "/api/v1/me/settings",
            new
            {
                displayName = "User",
                culture = "en",
                timeZoneId = "Mars/Olympus",
                theme = "system",
                weekStart = "Monday",
                defaultReminderMinutes = 15
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> CreateProjectAsync(HttpClient client, string name)
    {
        var create = await client.PostAsJsonAsync("/api/v1/projects", new { name });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await create.Content.ReadFromJsonAsync<CreatedResponse>(JsonOptions);
        created.ShouldNotBeNull();

        return created.Id;
    }

    private async Task<Guid> CreateTaskAsync(
        HttpClient client,
        string title,
        DateOnly? dueDate,
        Guid? projectId)
    {
        var create = await client.PostAsJsonAsync("/api/v1/tasks", new { title });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await create.Content.ReadFromJsonAsync<CreatedResponse>(JsonOptions);
        created.ShouldNotBeNull();

        var update = await client.PutAsJsonAsync(
            $"/api/v1/tasks/{created.Id}",
            new
            {
                title,
                notes = (string?)null,
                projectId,
                priority = "None",
                dueDate = dueDate?.ToString("yyyy-MM-dd"),
                dueTime = (string?)null,
                reminderOffsetMinutes = (int?)null
            });

        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        return created.Id;
    }

    private async Task<HttpClient> CreateBearerClientAsync(string email)
    {
        var client = fixture.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = Password });

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        tokens.ShouldNotBeNull();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return client;
    }

    private async Task<(Guid UserId, string Email, string Password)> CreateUserAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var email = $"api-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            DisplayName = "API User",
            Culture = "en",
            TimeZoneId = "UTC",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, Password);
        result.Succeeded.ShouldBeTrue(
            string.Join(", ", result.Errors.Select(error => error.Description)));

        return (user.Id, email, Password);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        match.Success.ShouldBeTrue("The antiforgery token was not found in the form.");
        return match.Groups[1].Value;
    }

    private sealed record TokenResponse(
        string TokenType,
        string AccessToken,
        long ExpiresIn,
        string RefreshToken);

    private sealed record CreatedResponse(Guid Id);
}
