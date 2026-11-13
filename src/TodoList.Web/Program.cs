using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;
using Scalar.AspNetCore;
using TodoList.Application;
using TodoList.Infrastructure;
using TodoList.Infrastructure.Identity;
using TodoList.Infrastructure.Persistence;
using TodoList.Web.Api;
using TodoList.Web.Components;
using TodoList.Web.Security;
using TodoList.Web.Services;
using IPNetwork = System.Net.IPNetwork;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        options.UseUtcTimestamp = true;
        options.JsonWriterOptions = new JsonWriterOptions { Indented = false };
    });
}

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.AddMudServices();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<SecurityHeadersOptions>(
    builder.Configuration.GetSection(SecurityHeadersOptions.SectionName));

builder.Services.AddAntiforgery(options =>
{
    // Plain-HTTP development keeps working, while production never emits a
    // cookie that a browser would also send over an unencrypted connection.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

// Forwarded headers are opt-in: without a trusted proxy any caller could spoof
// the client IP that the auth limiter and the audit trail depend on.
var forwardedHeaders = builder.Configuration.GetSection("ForwardedHeaders");
var useForwardedHeaders = forwardedHeaders.GetValue("Enabled", false);

if (useForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = Math.Max(1, forwardedHeaders.GetValue<int?>("ForwardLimit") ?? 1);

        var knownProxies = forwardedHeaders.GetSection("KnownProxies").Get<string[]>() ?? [];
        var knownNetworks = forwardedHeaders.GetSection("KnownNetworks").Get<string[]>() ?? [];

        if (knownProxies.Length > 0 || knownNetworks.Length > 0)
        {
            // An explicit trust list replaces the loopback-only defaults.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var proxy in knownProxies)
            {
                if (!IPAddress.TryParse(proxy, out var address))
                {
                    throw new InvalidOperationException(
                        $"ForwardedHeaders:KnownProxies contains an invalid IP address: '{proxy}'.");
                }

                options.KnownProxies.Add(address);
            }

            foreach (var network in knownNetworks)
            {
                if (!IPNetwork.TryParse(network, out var parsed))
                {
                    throw new InvalidOperationException(
                        $"ForwardedHeaders:KnownNetworks contains an invalid CIDR range: '{network}'.");
                }

                options.KnownIPNetworks.Add(parsed);
            }
        }
    });
}

// Authentication attempts share one fixed-window policy keyed by the client IP.
// The limiter runs only inside the authentication branch below, so the global
// limiter never sees page loads, static assets, or the rest of the API.
var authRateLimit = builder.Configuration.GetSection("Auth:RateLimit");
var authPermitLimit = Math.Max(1, authRateLimit.GetValue("PermitLimit", 10));
var authWindowSeconds = Math.Max(1, authRateLimit.GetValue("WindowSeconds", 60));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authPermitLimit,
            Window = TimeSpan.FromSeconds(authWindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    };
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(allowIntegerValues: false)));

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("The 'Default' connection string is not configured.");

builder.Services.AddInfrastructure(connectionString, builder.Configuration, builder.Environment);
builder.Services.AddApplication();
builder.Services.AddScoped<ICurrentUserProvider, CurrentUserProvider>();
builder.Services.AddScoped<ErrorLocalizer>();
builder.Services.AddScoped<ThemePreference>();
builder.Services.AddScoped<ApiUserContextFactory>();

builder.Services
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

builder.Services.AddAuthentication()
    .AddBearerToken(IdentityConstants.BearerScheme);

builder.Services.Configure<BearerTokenOptions>(IdentityConstants.BearerScheme, options =>
{
    options.BearerTokenExpiration = TimeSpan.FromDays(7);
    options.RefreshTokenExpiration = TimeSpan.FromDays(30);
});

var app = builder.Build();

// Must run before HSTS, redirection, and authentication so the request looks
// like the external one (scheme https, real client IP).
if (useForwardedHeaders)
{
    app.UseForwardedHeaders();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();

    if (string.IsNullOrWhiteSpace(app.Configuration["DataProtection:KeysPath"]))
    {
        app.Logger.LogWarning(
            "DataProtection:KeysPath is not configured. Keys live inside the container and " +
            "will be lost when it is recreated, invalidating cookies, antiforgery tokens, and bearer tokens.");
    }

    var allowedHosts = app.Configuration["AllowedHosts"];
    if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Contains('*', StringComparison.Ordinal))
    {
        app.Logger.LogWarning(
            "AllowedHosts accepts every host name. Set ALLOWED_HOSTS to the public host names " +
            "before exposing the app, so a spoofed Host header cannot reach it.");
    }
}

// Registered after the exception handler so both normal and re-executed error
// responses carry the headers. Forwarded headers stay outside this middleware
// because they do not write response headers.
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Configuration.GetValue("Reminders:Enabled", true)
    && string.IsNullOrWhiteSpace(app.Configuration["Email:Host"]))
{
    app.Logger.LogWarning(
        "Reminders are enabled but Email:Host is not configured. Reminder emails are written " +
        "to the log by the fallback sender; configure Email:* or set Reminders:Enabled=false.");
}

var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("ru") };

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
    RequestCultureProviders =
    [
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    ]
});

app.UseHttpsRedirection();

// Authentication attempts are the only throttled surface: page loads, assets,
// and the REST API stay unaffected so the limiter cannot slow the normal UI.
// Registered above the status-code pages so a rejection keeps a bare 429.
app.UseWhen(IsAuthenticationRequest, branch => branch.UseRateLimiter());

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar");
}

app.MapAuthEndpoints();

var api = app
    .MapGroup("/api/v1")
    .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = IdentityConstants.BearerScheme });

api.MapTaskEndpoints()
    .MapProjectEndpoints()
    .MapTagEndpoints()
    .MapMeEndpoints()
    .MapDataEndpoints();

app.MapGet("/culture/set", (
    string culture,
    string? redirect,
    HttpContext httpContext,
    TimeProvider timeProvider) =>
{
    var selected = supportedCultures.FirstOrDefault(x => x.Name == culture)?.Name ?? "en";

    httpContext.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(selected)),
        new CookieOptions
        {
            Expires = timeProvider.GetUtcNow().AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps
        });

    return Results.LocalRedirect(redirect ?? "/");
});

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateDatabaseAsync();

    if (app.Configuration.GetValue("Seed:Demo", true))
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
    }
}

app.Run();

static bool IsAuthenticationRequest(HttpContext context)
{
    if (!HttpMethods.IsPost(context.Request.Method))
    {
        return false;
    }

    var path = context.Request.Path;

    return path.Equals("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/account/login", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/account/register", StringComparison.OrdinalIgnoreCase);
}

public partial class Program;
