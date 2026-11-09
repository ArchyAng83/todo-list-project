using Bunit;
using Microsoft.AspNetCore.Components;
using Shouldly;
using TodoList.Application.Tags;
using TodoList.Application.Tasks;
using TodoList.Domain.Tasks;
using TodoList.Web.Components.Tasks;

namespace TodoList.ComponentTests;

[Collection(ComponentCollection.Name)]
public sealed class TaskListTests : ComponentTestContext
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    [Fact]
    public void TaskList_Loading_RendersProgressAndNoRows()
    {
        var cut = Render<TaskList>(parameters => parameters
            .Add(component => component.IsLoading, true));

        cut.FindComponents<MudBlazor.MudProgressLinear>().ShouldHaveSingleItem();
        cut.FindAll(".task-row").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("No tasks yet");
    }

    [Fact]
    public void TaskList_Error_RendersAlertAndInvokesRetry()
    {
        var retried = false;

        var cut = Render<TaskList>(parameters => parameters
            .Add(component => component.Error, "Unable to load tasks.")
            .Add(component => component.OnRetry, () => retried = true));

        cut.Markup.ShouldContain("Unable to load tasks.");

        cut.Find("button").Click();

        retried.ShouldBeTrue();
    }

    [Fact]
    public void TaskList_EmptyWithoutFilters_RendersOnboardingState()
    {
        var cut = Render<TaskList>(parameters => parameters
            .Add(component => component.Tasks, []));

        cut.Markup.ShouldContain("No tasks yet");
        cut.Markup.ShouldContain("Add your first task above to get started.");
    }

    [Fact]
    public void TaskList_EmptyWithFilters_RendersFilteredEmptyState()
    {
        var cut = Render<TaskList>(parameters => parameters
            .Add(component => component.Tasks, [])
            .Add(component => component.HasActiveFilters, true));

        cut.Markup.ShouldContain("No tasks match these filters.");
        cut.Markup.ShouldNotContain("Add your first task above to get started.");
    }

    [Fact]
    public void TaskList_Tasks_RendersTitleMetadataAndTags()
    {
        var cut = Render<TaskList>(parameters => parameters
            .Add(component => component.Today, Today)
            .Add(component => component.Tasks, new[]
            {
                CreateTask(
                    "Write the case study",
                    projectName: "Portfolio",
                    priority: TaskPriority.High,
                    dueDate: Today,
                    tags: [new TagRef(Guid.NewGuid(), "urgent", "#EF4444")],
                    checklistTotal: 3,
                    checklistCompleted: 1),
                CreateTask("Buy groceries")
            }));

        cut.FindAll(".task-row").Count.ShouldBe(2);
        cut.Markup.ShouldContain("Write the case study");
        cut.Markup.ShouldContain("Portfolio");
        cut.Markup.ShouldContain("High");
        cut.Markup.ShouldContain("urgent");
        cut.Markup.ShouldContain("1/3");
        cut.FindAll(".tag-chip").ShouldHaveSingleItem();
    }

    [Fact]
    public void TaskList_OverdueTodo_IsMarkedButDoneTaskIsNot()
    {
        var cut = Render<TaskList>(parameters => parameters
            .Add(component => component.Today, Today)
            .Add(component => component.Tasks, new[]
            {
                CreateTask("Overdue", dueDate: Today.AddDays(-1)),
                CreateTask(
                    "Already done",
                    status: TodoTaskStatus.Done,
                    dueDate: Today.AddDays(-2))
            }));

        cut.FindAll(".task-meta-item.is-overdue").ShouldHaveSingleItem();
        cut.FindAll(".task-row.is-done").ShouldHaveSingleItem();
    }

    [Fact]
    public void TaskList_Toggle_InvokesCallbackWithTask()
    {
        var task = CreateTask("Toggle me");
        TaskListItem? toggled = null;

        var cut = Render<TaskList>(parameters => parameters
            .Add(component => component.Tasks, new[] { task })
            .Add(component => component.OnToggleDone, value => toggled = value));

        cut.Find("input[type=checkbox]").Change(true);

        toggled.ShouldNotBeNull();
        toggled.Id.ShouldBe(task.Id);
    }

    [Fact]
    public void TaskList_EditAndDelete_InvokeCallbacks()
    {
        var task = CreateTask("Edit me");
        TaskListItem? edited = null;
        TaskListItem? deleted = null;

        var cut = Render<TaskList>(parameters => parameters
            .Add(component => component.Tasks, new[] { task })
            .Add(component => component.OnEdit, value => edited = value)
            .Add(component => component.OnDelete, value => deleted = value));

        cut.Find("button[aria-label='Edit']").Click();
        cut.Find("button[aria-label='Delete']").Click();

        edited.ShouldNotBeNull();
        edited.Id.ShouldBe(task.Id);
        deleted.ShouldNotBeNull();
        deleted.Id.ShouldBe(task.Id);
    }

    private static TaskListItem CreateTask(
        string title,
        TodoTaskStatus status = TodoTaskStatus.Todo,
        TaskPriority priority = TaskPriority.None,
        DateOnly? dueDate = null,
        string? projectName = null,
        IReadOnlyList<TagRef>? tags = null,
        int checklistTotal = 0,
        int checklistCompleted = 0)
        => new(
            Guid.NewGuid(),
            title,
            status,
            priority,
            dueDate,
            null,
            projectName is null ? null : Guid.NewGuid(),
            projectName,
            tags ?? [],
            checklistTotal,
            checklistCompleted);
}
