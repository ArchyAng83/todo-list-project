using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TodoList.Application.DataTransfer;
using TodoList.Application.Tasks;
using TodoList.Domain.Tasks;
using TodoList.Infrastructure.Identity;
using TodoList.Testing;

namespace TodoList.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class DataApiTests(TodoListAppFixture fixture)
{
    private const string Password = "Password1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Data_ExportJson_ReturnsVersionedDocument()
    {
        using var client = await CreateBearerClientAsync();

        (await client.PostAsJsonAsync("/api/v1/tasks", new { title = "Export me" }))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await client.GetAsync("/api/v1/data/export");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentDisposition?.FileName.ShouldNotBeNull();
        response.Content.Headers.ContentDisposition!.FileName!.ShouldContain(".json");

        var document = await response.Content.ReadFromJsonAsync<ExportDocument>(JsonOptions);

        document.ShouldNotBeNull();
        document.Version.ShouldBe(ExportDocument.CurrentVersion);
        document.Tasks.ShouldHaveSingleItem().Title.ShouldBe("Export me");
    }

    [Fact]
    public async Task Data_ExportCsv_ReturnsAttachmentWithUtf8Bom()
    {
        using var client = await CreateBearerClientAsync();

        (await client.PostAsJsonAsync("/api/v1/tasks", new { title = "CSV, \"quoted\"" }))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await client.GetAsync("/api/v1/data/export/csv");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
        response.Content.Headers.ContentDisposition!.FileName!.ShouldContain(".csv");

        var bytes = await response.Content.ReadAsByteArrayAsync();

        bytes.Take(3).ShouldBe([0xEF, 0xBB, 0xBF]);

        var text = Encoding.UTF8.GetString(bytes);
        text.ShouldContain("CSV");
        text.ShouldContain("Todo");
    }

    [Fact]
    public async Task Data_ImportJson_RoundTripsTasks()
    {
        using var client = await CreateBearerClientAsync();
        var document = CreateDocument("Imported via API");

        var response = await client.PostAsJsonAsync("/api/v1/data/import", document, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var summary = await response.Content.ReadFromJsonAsync<ImportSummary>(JsonOptions);

        summary.ShouldNotBeNull();
        summary.TasksCreated.ShouldBe(1);
        summary.ProjectsCreated.ShouldBe(1);
        summary.TagsCreated.ShouldBe(1);

        var tasks = await client.GetFromJsonAsync<List<TaskListItem>>(
            "/api/v1/tasks",
            JsonOptions);

        tasks.ShouldNotBeNull();
        tasks.ShouldHaveSingleItem().Title.ShouldBe("Imported via API");
    }

    [Fact]
    public async Task Data_ImportUnsupportedVersion_ReturnsBadRequest()
    {
        using var client = await CreateBearerClientAsync();
        var document = CreateDocument("Bad version") with
        {
            Version = ExportDocument.CurrentVersion + 1
        };

        var response = await client.PostAsJsonAsync("/api/v1/data/import", document, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Data_ExportAnonymous_ReturnsUnauthorized()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/data/export");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static ExportDocument CreateDocument(string title)
        => new(
            ExportDocument.CurrentVersion,
            DateTimeOffset.UtcNow,
            [new ExportProject(Guid.CreateVersion7(), "API project", "#4F46E5", false, 0)],
            [new ExportTag(Guid.CreateVersion7(), "api-tag", "#EF4444")],
            [
                new ExportTask(
                    Guid.CreateVersion7(),
                    null,
                    title,
                    null,
                    TodoTaskStatus.Todo,
                    TaskPriority.Medium,
                    null,
                    null,
                    null,
                    RecurrenceFrequency.None,
                    1,
                    null,
                    0,
                    DateTimeOffset.UtcNow,
                    null,
                    [],
                    [])
            ]);

    private async Task<HttpClient> CreateBearerClientAsync()
    {
        var email = $"data-api-{Guid.NewGuid():N}@example.com";

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                UserName = email,
                Email = email,
                DisplayName = "Data API User",
                Culture = "en",
                TimeZoneId = "UTC",
                CreatedAt = DateTimeOffset.UtcNow
            };

            var created = await userManager.CreateAsync(user, Password);
            created.Succeeded.ShouldBeTrue(
                string.Join(", ", created.Errors.Select(error => error.Description)));
        }

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

    private sealed record TokenResponse(
        string TokenType,
        string AccessToken,
        long ExpiresIn,
        string RefreshToken);
}
