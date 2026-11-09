using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Playwright;
using TodoList.Testing;

namespace TodoList.E2ETests;

/// <summary>
/// Starts PostgreSQL in a container and the real web application on Kestrel,
/// then drives it with a headless Chromium instance.
/// </summary>
public sealed class E2EFixture : IAsyncLifetime
{
    private readonly PostgresContainerFixture _postgres = new();
    private Process? _app;
    private Task<string>? _stderr;
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;

    public string BaseUrl { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();

        BaseUrl = $"http://127.0.0.1:{GetFreePort()}";
        StartApplication();
        await WaitForApplicationAsync();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public Task<IBrowserContext> NewContextAsync()
        => Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            IgnoreHTTPSErrors = true
        });

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_app is not null)
        {
            if (!_app.HasExited)
            {
                _app.Kill(entireProcessTree: true);
                await _app.WaitForExitAsync();
            }

            _app.Dispose();
        }

        await _postgres.DisposeAsync();
    }

    private void StartApplication()
    {
        var repositoryRoot = FindRepositoryRoot();
        var assemblyName = Path.Combine(
            repositoryRoot,
            "src",
            "TodoList.Web",
            "bin",
            BuildConfiguration,
            "net10.0",
            "TodoList.Web.dll");

        if (!File.Exists(assemblyName))
        {
            throw new FileNotFoundException(
                "The web application was not built. Build the solution before running E2E tests.",
                assemblyName);
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.Combine(repositoryRoot, "src", "TodoList.Web"),
            UseShellExecute = false,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add(assemblyName);
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(BaseUrl);

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ConnectionStrings__Default"] = _postgres.ConnectionString;
        startInfo.Environment["Seed__Demo"] = "true";
        startInfo.Environment["Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command"] = "Warning";

        _app = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The web application process could not be started.");

        _stderr = DrainAsync(_app.StandardError);
    }

    private async Task WaitForApplicationAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTimeOffset.UtcNow.AddSeconds(120);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_app!.HasExited)
            {
                var error = await _stderr!;
                throw new InvalidOperationException(
                    $"The web application exited with code {_app.ExitCode}.{Environment.NewLine}{error}");
            }

            try
            {
                var response = await client.GetAsync($"{BaseUrl}/health/ready");

                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Kestrel has not started listening yet.
            }
            catch (TaskCanceledException)
            {
                // The request timed out while the application was starting.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException("The web application did not become healthy within two minutes.");
    }

    private static async Task<string> DrainAsync(StreamReader reader)
    {
        var output = new StringBuilder();
        string? line;

        while ((line = await reader.ReadLineAsync()) is not null)
        {
            output.AppendLine(line);
        }

        return output.ToString();
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TodoList.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root containing TodoList.sln was not found.");
    }

    private static string BuildConfiguration =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif
}
