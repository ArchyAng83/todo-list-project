using Shouldly;
using TodoList.Testing;

namespace TodoList.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class SecurityHeadersTests(TodoListAppFixture fixture)
{
    [Fact]
    public async Task HomePage_ReturnsBrowserHardeningHeaders()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/");

        response.IsSuccessStatusCode.ShouldBeTrue();

        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
        response.Headers.GetValues("Referrer-Policy").ShouldContain("strict-origin-when-cross-origin");

        var csp = string.Join(' ', response.Headers.GetValues("Content-Security-Policy"));
        csp.ShouldContain("default-src 'self'");
        csp.ShouldContain("frame-ancestors 'none'");
    }
}
