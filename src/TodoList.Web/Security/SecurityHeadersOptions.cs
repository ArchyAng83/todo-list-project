namespace TodoList.Web.Security;

/// <summary>
/// Response headers applied to every request. The defaults are deliberately
/// conservative but still allow Blazor Server and MudBlazor to work:
/// server-rendered components use inline styles, and the SignalR circuit opens
/// a WebSocket connection back to the same origin.
/// </summary>
public sealed class SecurityHeadersOptions
{
    public const string SectionName = "SecurityHeaders";

    public bool Enabled { get; set; } = true;

    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    public string PermissionsPolicy { get; set; } =
        "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

    public string ContentSecurityPolicy { get; set; } =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "frame-ancestors 'none'; " +
        "object-src 'none'; " +
        "form-action 'self'; " +
        "img-src 'self' data:; " +
        "font-src 'self' data:; " +
        "style-src 'self' 'unsafe-inline'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "connect-src 'self' ws: wss:";
}
