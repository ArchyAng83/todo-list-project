using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace TodoList.ComponentTests;

/// <summary>
/// Shared bUnit setup: real MudBlazor services, the application's resource
/// files, and a deterministic UI culture so assertions are stable.
/// </summary>
public abstract class ComponentTestContext : BunitContext
{
    protected ComponentTestContext()
    {
        var culture = CultureInfo.GetCultureInfo("en");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        Services.AddLocalization(options => options.ResourcesPath = "Resources");
        Services.AddMudServices();

        // Components under test may call into MudBlazor's JavaScript helpers.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>
    /// MudBlazor services implement <see cref="IAsyncDisposable"/> but not
    /// <see cref="IDisposable"/>, and xUnit 2 disposes test classes
    /// synchronously. Dispose the bUnit context asynchronously instead.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            base.Dispose(disposing);
            return;
        }

        DisposeAsyncCore().AsTask().GetAwaiter().GetResult();
    }
}
