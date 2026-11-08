using System.Net;
using System.Text.RegularExpressions;
using Shouldly;
using TodoList.Testing;

namespace TodoList.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class AuthEndpointsTests(TodoListAppFixture fixture)
{
    [Fact]
    public async Task LoginPage_RendersForm()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/account/login");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("__RequestVerificationToken");
        html.ShouldContain("/account/register");
    }

    [Fact]
    public async Task ProtectedPage_RedirectsAnonymousUserToLogin()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        var response = await client.GetAsync("/tasks");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.PathAndQuery.ShouldStartWith("/account/login");
    }

    [Fact]
    public async Task Register_CreatesUserSignsInAndRedirectsToTasks()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var registerPage = await client.GetStringAsync("/account/register");
        var token = ExtractAntiforgeryToken(registerPage);

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "register",
            ["__RequestVerificationToken"] = token,
            ["Input.DisplayName"] = "Integration User",
            ["Input.Email"] = email,
            ["Input.Password"] = "Password1",
            ["Input.ConfirmPassword"] = "Password1"
        });

        var registerResponse = await client.PostAsync("/account/register", form);

        registerResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        registerResponse.Headers.Location.ShouldNotBeNull();
        registerResponse.Headers.Location.PathAndQuery.ShouldBe("/tasks");

        var tasksResponse = await client.GetAsync("/tasks");

        tasksResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await tasksResponse.Content.ReadAsStringAsync()).ShouldContain(email);
    }

    [Fact]
    public async Task Register_WithMismatchedPasswords_ShowsValidationError()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);
        var registerPage = await client.GetStringAsync("/account/register");
        var token = ExtractAntiforgeryToken(registerPage);

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_handler"] = "register",
            ["__RequestVerificationToken"] = token,
            ["Input.DisplayName"] = "Integration User",
            ["Input.Email"] = $"user-{Guid.NewGuid():N}@example.com",
            ["Input.Password"] = "Password1",
            ["Input.ConfirmPassword"] = "Password2"
        });

        var response = await client.PostAsync("/account/register", form);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("password");
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

        match.Success.ShouldBeTrue("The antiforgery token was not found in the form.");
        return match.Groups[1].Value;
    }
}
