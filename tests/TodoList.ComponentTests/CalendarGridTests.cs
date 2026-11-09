using Bunit;
using Shouldly;
using TodoList.Application.Tasks;
using TodoList.Domain.Tasks;
using TodoList.Web.Components.Tasks;

namespace TodoList.ComponentTests;

[Collection(ComponentCollection.Name)]
public sealed class CalendarGridTests : ComponentTestContext
{
    private static readonly DateOnly Month = new(2026, 11, 1);
    private static readonly DateOnly Today = new(2026, 11, 9);

    [Fact]
    public void CalendarGrid_Loading_RendersProgressAndNoGrid()
    {
        var cut = Render<CalendarGrid>(parameters => parameters
            .Add(component => component.Month, Month)
            .Add(component => component.IsLoading, true));

        cut.FindComponents<MudBlazor.MudProgressLinear>().ShouldHaveSingleItem();
        cut.FindAll(".calendar-grid").ShouldBeEmpty();
    }

    [Fact]
    public void CalendarGrid_Error_RendersAlertAndInvokesRetry()
    {
        var retried = false;

        var cut = Render<CalendarGrid>(parameters => parameters
            .Add(component => component.Month, Month)
            .Add(component => component.Error, "Unable to load the calendar.")
            .Add(component => component.OnRetry, () => retried = true));

        cut.Markup.ShouldContain("Unable to load the calendar.");

        cut.Find("button").Click();

        retried.ShouldBeTrue();
    }

    [Fact]
    public void CalendarGrid_MondayWeekStart_RendersSixWeeksAndWeekdayOrder()
    {
        var cut = Render<CalendarGrid>(parameters => parameters
            .Add(component => component.Month, Month)
            .Add(component => component.Today, Today)
            .Add(component => component.WeekStart, DayOfWeek.Monday));

        cut.Find(".calendar-month").TextContent.Trim().ShouldBe("November 2026");

        cut.FindAll(".calendar-weekday")
            .Select(cell => cell.TextContent.Trim())
            .ShouldBe(["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]);

        var days = cut.FindAll(".calendar-day");

        days.Count.ShouldBe(42);
        days[0].GetAttribute("data-date").ShouldBe("2026-10-26");
        days[41].GetAttribute("data-date").ShouldBe("2026-12-06");
        days[0].ClassList.ShouldContain("is-outside");
        days[6].ClassList.ShouldNotContain("is-outside");
    }

    [Fact]
    public void CalendarGrid_SundayWeekStart_StartsOnTheFirstOfTheMonth()
    {
        var cut = Render<CalendarGrid>(parameters => parameters
            .Add(component => component.Month, Month)
            .Add(component => component.Today, Today)
            .Add(component => component.WeekStart, DayOfWeek.Sunday));

        cut.FindAll(".calendar-weekday")
            .Select(cell => cell.TextContent.Trim())
            .ShouldBe(["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"]);

        var days = cut.FindAll(".calendar-day");

        days[0].GetAttribute("data-date").ShouldBe("2026-11-01");
        days[0].ClassList.ShouldNotContain("is-outside");
    }

    [Fact]
    public void CalendarGrid_PlacesTasksOnTheirDueDateWithStateStyles()
    {
        var dueTask = CreateTask(
            "Ship the calendar",
            new DateOnly(2026, 11, 9),
            new TimeOnly(9, 30),
            TaskPriority.High);
        var doneTask = CreateTask(
            "Already done",
            new DateOnly(2026, 11, 10), status: TodoTaskStatus.Done);
        var overdueTask = CreateTask(
            "Missed it",
            new DateOnly(2026, 11, 2));

        var cut = Render<CalendarGrid>(parameters => parameters
            .Add(component => component.Month, Month)
            .Add(component => component.Today, Today)
            .Add(component => component.WeekStart, DayOfWeek.Monday)
            .Add(component => component.Tasks, [dueTask, doneTask, overdueTask]));

        var dueCell = cut.Find("[data-date='2026-11-09']");

        dueCell.ClassList.ShouldContain("is-today");
        dueCell.TextContent.ShouldContain("Ship the calendar");
        dueCell.TextContent.ShouldContain("9:30");

        cut.Find("[data-date='2026-11-10']").TextContent.ShouldContain("Already done");
        cut.Find("[data-date='2026-11-10'] .calendar-task").ClassList.ShouldContain("is-done");
        cut.Find("[data-date='2026-11-02'] .calendar-task").ClassList.ShouldContain("is-overdue");
    }

    [Fact]
    public void CalendarGrid_MoreThanThreeTasks_ShowsOverflowCount()
    {
        var tasks = Enumerable.Range(1, 5)
            .Select(index => CreateTask($"Task {index}", new DateOnly(2026, 11, 12)))
            .ToList();

        var cut = Render<CalendarGrid>(parameters => parameters
            .Add(component => component.Month, Month)
            .Add(component => component.Today, Today)
            .Add(component => component.Tasks, tasks));

        var cell = cut.Find("[data-date='2026-11-12']");

        cell.QuerySelectorAll(".calendar-task").Length.ShouldBe(3);
        cell.QuerySelector(".calendar-more")!.TextContent.Trim().ShouldBe("and 2 more");
    }

    [Fact]
    public void CalendarGrid_DayClick_InvokesDaySelectedWithTheDate()
    {
        DateOnly? selected = null;

        var cut = Render<CalendarGrid>(parameters => parameters
            .Add(component => component.Month, Month)
            .Add(component => component.Today, Today)
            .Add(component => component.OnDaySelected, date => selected = date));

        cut.Find("[data-date='2026-11-15']").Click();

        selected.ShouldBe(new DateOnly(2026, 11, 15));
    }

    [Fact]
    public void CalendarGrid_TaskClick_EditsTheTaskWithoutCreatingOnTheDay()
    {
        var task = CreateTask("Edit me", new DateOnly(2026, 11, 15));
        TaskListItem? edited = null;
        DateOnly? selected = null;

        var cut = Render<CalendarGrid>(parameters => parameters
            .Add(component => component.Month, Month)
            .Add(component => component.Today, Today)
            .Add(component => component.Tasks, [task])
            .Add(component => component.OnEditTask, value => edited = value)
            .Add(component => component.OnDaySelected, date => selected = date));

        cut.Find("[data-date='2026-11-15'] .calendar-task").Click();

        edited.ShouldNotBeNull();
        edited.Id.ShouldBe(task.Id);
        selected.ShouldBeNull();
    }

    [Fact]
    public void CalendarGrid_MonthControls_InvokeCallbacks()
    {
        var previous = 0;
        var next = 0;
        var today = 0;

        var cut = Render<CalendarGrid>(parameters => parameters
            .Add(component => component.Month, Month)
            .Add(component => component.Today, Today)
            .Add(component => component.OnPreviousMonth, () => previous++)
            .Add(component => component.OnNextMonth, () => next++)
            .Add(component => component.OnToday, () => today++));

        cut.Find("button[aria-label='Previous month']").Click();
        cut.Find("button[aria-label='Next month']").Click();
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Today").Click();

        previous.ShouldBe(1);
        next.ShouldBe(1);
        today.ShouldBe(1);
    }

    private static TaskListItem CreateTask(
        string title,
        DateOnly? dueDate,
        TimeOnly? dueTime = null,
        TaskPriority priority = TaskPriority.None,
        TodoTaskStatus status = TodoTaskStatus.Todo)
        => new(
            Guid.NewGuid(),
            title,
            status,
            priority,
            dueDate,
            dueTime,
            null,
            null,
            [],
            0,
            0);
}
