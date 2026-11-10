using System.Text;
using System.Text.Json;
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
    public async Task Board_MovesCardWithTheMenu_AndPersistsAfterReload()
    {
        await using var context = await fixture.NewContextAsync();
        var page = await SignInAsync(context);
        var title = $"E2E board {Guid.NewGuid():N}";

        await page.GetByPlaceholder("Add a task and press Add").FillAsync(title);
        await page.GetByRole(AriaRole.Button, new() { Name = "Add task" }).ClickAsync();
        await Expect(page.GetByText(title)).ToBeVisibleAsync();

        await OpenBoardAsync(page);

        var card = page.Locator(".board-card").Filter(new() { HasTextString = title });

        await Expect(card).ToHaveCountAsync(1);
        await card.Locator("summary").ClickAsync();
        await card.GetByRole(AriaRole.Button, new() { Name = "Move to \"Done\"" }).ClickAsync();

        var todo = page.Locator(".board-column[data-status='Todo']");
        var done = page.Locator(".board-column[data-status='Done']");

        await Expect(done.GetByText(title)).ToBeVisibleAsync();
        await Expect(todo.GetByText(title)).ToHaveCountAsync(0);

        await page.ReloadAsync();

        await Expect(page.Locator(".board-column[data-status='Done']").GetByText(title))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Board_DragsCardToAnotherColumn_AndPersistsAfterReload()
    {
        await using var context = await fixture.NewContextAsync();
        var page = await SignInAsync(context);
        const string title = "Plan the weekly review";

        await OpenBoardAsync(page);

        var source = page
            .Locator(".board-column[data-status='Todo'] .board-card")
            .Filter(new() { HasTextString = title });
        var target = page.Locator(".board-column[data-status='InProgress'] .board-column-body");

        await source.DragToAsync(target);

        await Expect(page.Locator(".board-column[data-status='InProgress']").GetByText(title))
            .ToBeVisibleAsync();

        await page.ReloadAsync();

        await Expect(page.Locator(".board-column[data-status='InProgress']").GetByText(title))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Calendar_ShowsTasksOnTheirDueDates()
    {
        await using var context = await fixture.NewContextAsync();
        var page = await SignInAsync(context);

        await OpenCalendarAsync(page);

        var today = page.Locator(".calendar-day.is-today");

        await Expect(today).ToHaveCountAsync(1);
        await Expect(today.GetByText("Buy groceries")).ToBeVisibleAsync();
        await Expect(today.GetByText("Write portfolio case study")).ToBeVisibleAsync();
        await Expect(page.GetByText("Read the ASP.NET Core docs")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Calendar_NavigatesBetweenMonths()
    {
        await using var context = await fixture.NewContextAsync();
        var page = await SignInAsync(context);

        await OpenCalendarAsync(page);

        var title = page.Locator(".calendar-month");
        var initial = (await title.TextContentAsync())!.Trim();

        await page.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();
        await Expect(title).Not.ToHaveTextAsync(initial);

        await page.GetByRole(AriaRole.Button, new() { Name = "Previous month" }).ClickAsync();
        await Expect(title).ToHaveTextAsync(initial);
    }

    [Fact]
    public async Task Calendar_CreatesTaskOnClickedDay_AndPersistsAfterReload()
    {
        await using var context = await fixture.NewContextAsync();
        var page = await SignInAsync(context);

        await OpenCalendarAsync(page);

        var title = $"E2E calendar {Guid.NewGuid():N}";
        var day = page.Locator(".calendar-day.is-today");
        var date = (await day.GetAttributeAsync("data-date"))!;

        // Click the day number so the click cannot land on an existing task chip.
        await day.Locator(".calendar-day-number").ClickAsync();

        var dialog = page.Locator(".mud-dialog");

        await Expect(dialog).ToBeVisibleAsync();
        await dialog.GetByLabel("Title").FillAsync(title);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add task" }).ClickAsync();

        var chip = page.Locator($".calendar-day[data-date='{date}']").GetByText(title);

        await Expect(chip).ToBeVisibleAsync();

        await page.ReloadAsync();

        await Expect(page.Locator($".calendar-day[data-date='{date}']").GetByText(title))
            .ToBeVisibleAsync();
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

    [Fact]
    public async Task RecurringTask_CompletingIt_CreatesTheNextOccurrence()
    {
        await using var context = await fixture.NewContextAsync();
        var page = await SignInAsync(context);

        var rows = page.Locator(".task-row").Filter(new() { HasTextString = "Weekly planning" });

        await Expect(rows).ToHaveCountAsync(1);
        await rows.First.Locator(".mud-checkbox").ClickAsync();

        var openRows = page
            .Locator(".task-row:not(.is-done)")
            .Filter(new() { HasTextString = "Weekly planning" });

        await Expect(openRows).ToHaveCountAsync(1);
        await Expect(page.Locator(".task-meta-item[title='Every week']").First)
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task DataPage_ExportsJsonAndCsv()
    {
        await using var context = await fixture.NewContextAsync();
        var page = await SignInAsync(context);

        await OpenDataAsync(page);

        var jsonDownload = await page.RunAndWaitForDownloadAsync(
            () => page.GetByRole(AriaRole.Button, new() { Name = "Download JSON" }).ClickAsync());

        Assert.EndsWith(".json", jsonDownload.SuggestedFilename);

        var jsonPath = (await jsonDownload.PathAsync())!;
        var json = await File.ReadAllTextAsync(jsonPath);

        using var document = JsonDocument.Parse(json);

        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
        Assert.True(document.RootElement.GetProperty("tasks").GetArrayLength() > 0);

        var csvDownload = await page.RunAndWaitForDownloadAsync(
            () => page.GetByRole(AriaRole.Button, new() { Name = "Download CSV" }).ClickAsync());

        Assert.EndsWith(".csv", csvDownload.SuggestedFilename);

        var csvPath = (await csvDownload.PathAsync())!;
        var bytes = await File.ReadAllBytesAsync(csvPath);

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3).ToArray());

        var csv = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"Title\"", csv);
    }

    [Fact]
    public async Task DataPage_ImportsJsonAndShowsTheTask()
    {
        await using var context = await fixture.NewContextAsync();
        var page = await SignInAsync(context);

        await OpenDataAsync(page);

        var title = $"E2E import {Guid.NewGuid():N}";
        var importPath = Path.Combine(
            Path.GetTempPath(),
            $"todolist-import-{Guid.NewGuid():N}.json");

        var json = $$"""
            {
              "version": 1,
              "exportedAt": "2026-11-09T00:00:00Z",
              "projects": [],
              "tags": [],
              "tasks": [
                {
                  "id": "00000000-0000-0000-0000-000000000000",
                  "title": "{{title}}",
                  "status": "Todo",
                  "priority": "Medium",
                  "recurrence": "None",
                  "recurrenceInterval": 1,
                  "sortOrder": 0,
                  "createdAt": "2026-11-09T00:00:00Z",
                  "tagIds": [],
                  "checklistItems": []
                }
              ]
            }
            """;

        try
        {
            await File.WriteAllTextAsync(importPath, json);

            await page.Locator("input[type=file]").SetInputFilesAsync(importPath);

            await Expect(page.GetByText("Import completed."))
                .ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Expect(page.GetByText("Imported 1 tasks"))
                .ToBeVisibleAsync(new() { Timeout = 30_000 });

            await page.GetByRole(AriaRole.Link, new() { Name = "Tasks", Exact = true }).ClickAsync();
            await Expect(page).ToHaveURLAsync(new Regex("/tasks$"));
            await Expect(page.GetByText(title))
                .ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
        finally
        {
            File.Delete(importPath);
        }
    }

    private static async Task OpenDataAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "Data", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/data$"));
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Data" }))
            .ToBeVisibleAsync();
        await WaitForBlazorAsync(page);
    }

    private static async Task OpenBoardAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "Board", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/board$"));
        await Expect(page.Locator(".board-card").First).ToBeVisibleAsync();
    }

    private static async Task OpenCalendarAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Link, new() { Name = "Calendar", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/calendar$"));
        await Expect(page.Locator(".calendar-day").First).ToBeVisibleAsync();
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
        await WaitForBlazorAsync(page);

        return page;
    }

    private static Task WaitForBlazorAsync(IPage page)
        => page.WaitForFunctionAsync(
            "() => [...document.querySelectorAll('button')]"
            + ".some(button => [...button.attributes].some(attribute => attribute.name.startsWith('_bl_')))",
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });
}
