using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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
using TodoList.Web.Services;

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

builder.Services.AddInfrastructure(connectionString, builder.Configuration);
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
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
            SameSite = SameSiteMode.Lax
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
