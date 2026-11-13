using Microsoft.Extensions.Options;

namespace TodoList.Web.Security;

/// <summary>
/// Adds browser hardening headers. The policy is intentionally additive:
/// endpoints that already set a header keep their value.
/// </summary>
public sealed class SecurityHeadersMiddleware(
    RequestDelegate next,
    IOptions<SecurityHeadersOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var settings = options.Value;

        if (settings.Enabled)
        {
            var headers = context.Response.Headers;

            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Referrer-Policy", settings.ReferrerPolicy);
            headers.TryAdd("Permissions-Policy", settings.PermissionsPolicy);

            if (!string.IsNullOrWhiteSpace(settings.ContentSecurityPolicy))
            {
                headers.TryAdd("Content-Security-Policy", settings.ContentSecurityPolicy);
            }
        }

        await next(context);
    }
}
