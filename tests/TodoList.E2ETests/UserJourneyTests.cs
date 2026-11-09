using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace TodoList.E2ETests;

[Collection(E2ECollection.Name)]
public sealed class UserJourneyTests(E2EFixture fixture)
{
    [Fact]
    public async Task AnonymousVisitor_OpensLoginPageFromLandingPage()
    {
        await using var context = await fixture.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync("/");

        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "What do you want to get done?" }))
            .ToBeVisibleAsync();

        await page.Locator("section.hero")
            .GetByRole(AriaRole.Link, new() { Name = "Sign in" })
            .ClickAsync();

        await Expect(page).ToHaveURLAsync(new Regex("/account/login$"));
        await Expect(page.Locator("#email")).ToBeVisibleAsync();
        await Expect(page.Locator("#password")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task DemoUser_SignsIn_AndSeesSeededTasks()
    {
        await using var context = await fixture.NewContextAsync();
        var page = await SignInAsync(context);

        await Expect(page.GetByText("Write portfolio case study")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Today", Exact = true }))
            .ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Upcoming", Exact = true }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task DemoUser_QuickAddsTask_AndSeesItInTheList()
    {
        await using var context = await fixture.NewContextAsync();
        var page = await SignInAsync(context);
        var title = $"E2E task {Guid.NewGuid():N}";

        await page.GetByPlaceholder("Add a task and press Add").FillAsync(title);
        await page.GetByRole(AriaRole.Button, new() { Name = "Add task" }).ClickAsync();

        await Expect(page.GetByText(title)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Search_FiltersTasksByName()
    {
        await using var context = await fixture.NewContextAsync();
        var page = await SignInAsync(context);

        await page.GetByLabel("Search tasks").FillAsync("groceries");

        await Expect(page.GetByText("Buy groceries")).ToBeVisibleAsync();
        await Expect(page.GetByText("Write portfolio case study")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task TodayView_ShowsOnlyTasksDueToday()
    {
        await using var context = await fixture.NewContextAsync();
        var page = await SignInAsync(context);

        await page.GetByRole(AriaRole.Button, new() { Name = "Today", Exact = true }).ClickAsync();

        await Expect(page.GetByText("Write portfolio case study")).ToBeVisibleAsync();
        await Expect(page.GetByText("Read the ASP.NET Core docs")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task CultureSwitcher_ChangesTheUiToRussian()
    {
        await using var context = await fixture.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync("/account/login");
        await page.GetByRole(AriaRole.Link, new() { Name = "RU", Exact = true }).ClickAsync();

        await Expect(page).ToHaveURLAsync(new Regex("/account/login$"));
        await Expect(page.Locator("html")).ToHaveAttributeAsync("lang", "ru");
        await Expect(page.Locator("h1")).ToHaveTextAsync("Войти");
    }

    private static async Task<IPage> SignInAsync(IBrowserContext context)
    {
        var page = await context.NewPageAsync();

        await page.GotoAsync("/account/login");
        await page.Locator("#email").FillAsync("demo@todolist.local");
        await page.Locator("#password").FillAsync("DemoPass123");
        await page.Locator("button[type=submit]").ClickAsync();

        await Expect(page).ToHaveURLAsync(new Regex("/tasks$"), new() { Timeout = 30_000 });

        // The prerendered HTML is fully clickable before the Blazor Server circuit
        // takes over, and a click in that window submits the quick-add form
        // natively. Blazor marks elements with event handlers using `_bl_*`
        // attributes, so wait for the quick-add button to become interactive.
        await page.WaitForFunctionAsync(
            "() => [...document.querySelectorAll('button[type=submit]')]"
            + ".some(button => [...button.attributes].some(attribute => attribute.name.startsWith('_bl_')))",
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });

        return page;
    }
}
