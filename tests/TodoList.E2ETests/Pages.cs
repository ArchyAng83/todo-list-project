using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace TodoList.E2ETests;

/// <summary>
/// Navigation helpers shared by the journeys. Every helper waits for the page to
/// settle because the prerendered HTML is clickable before the Blazor Server
/// circuit attaches its handlers.
/// </summary>
internal static class Pages
{
    public static async Task<IPage> OpenTasksAsync(IBrowserContext context)
    {
        var page = await context.NewPageAsync();

        await page.GotoAsync("/tasks");
        await Expect(page).ToHaveURLAsync(new Regex("/tasks$"));
        await WaitForBlazorAsync(page);

        return page;
    }

    public static async Task OpenBoardAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "Board", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/board$"));
        await Expect(page.Locator(".board-column").First).ToBeVisibleAsync();
    }

    public static async Task OpenCalendarAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "Calendar", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/calendar$"));
        await Expect(page.Locator(".calendar-day").First).ToBeVisibleAsync();
    }

    public static async Task OpenDataAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "Data", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/data$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Data" }))
            .ToBeVisibleAsync();
        await WaitForBlazorAsync(page);
    }

    /// <summary>
    /// A click before the circuit starts submits a form natively. Blazor marks
    /// elements with event handlers using <c>_bl_*</c> attributes, so wait until
    /// a button carries one.
    /// </summary>
    public static Task WaitForBlazorAsync(IPage page)
        => page.WaitForFunctionAsync(
            "() => [...document.querySelectorAll('button')]"
            + ".some(button => [...button.attributes].some(attribute => attribute.name.startsWith('_bl_')))",
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });
}
