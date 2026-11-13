using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Shouldly;
using TodoList.Application.Tags;
using TodoList.Application.Tasks;
using TodoList.Domain.Tasks;
using TodoList.Web.Components.Tasks;

namespace TodoList.ComponentTests;

[Collection(ComponentCollection.Name)]
public sealed class KanbanBoardTests : ComponentTestContext
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    [Fact]
    public void KanbanBoard_Loading_RendersProgressAndNoColumns()
    {
        var cut = Render<KanbanBoard>(parameters => parameters
            .Add(component => component.IsLoading, true));

        cut.FindComponents<MudBlazor.MudProgressLinear>().ShouldHaveSingleItem();
        cut.FindAll(".board-column").ShouldBeEmpty();
    }

    [Fact]
    public void KanbanBoard_Error_RendersAlertAndInvokesRetry()
    {
        var retried = false;

        var cut = Render<KanbanBoard>(parameters => parameters
            .Add(component => component.Error, "Unable to load the board.")
            .Add(component => component.OnRetry, () => retried = true));

        cut.Markup.ShouldContain("Unable to load the board.");

        cut.Find("button").Click();

        retried.ShouldBeTrue();
    }

    [Fact]
    public void KanbanBoard_RendersColumnsCardsAndMetadata()
    {
        var todo = CreateTask(
            "Write the case study",
            projectName: "Portfolio",
            priority: TaskPriority.High,
            dueDate: Today,
            tags: [new TagRef(Guid.NewGuid(), "urgent", "#EF4444")],
            checklistTotal: 3,
            checklistCompleted: 1);
        var inProgress = CreateTask("Review the pull request", TodoTaskStatus.InProgress);
        var done = CreateTask("Ship the release", TodoTaskStatus.Done);

        var cut = Render<KanbanBoard>(parameters => parameters
            .Add(component => component.Today, Today)
            .Add(component => component.Board, BoardWith([todo], [inProgress], [done])));

        var columns = cut.FindAll(".board-column");

        columns.Count.ShouldBe(3);
        columns[0].GetAttribute("data-status").ShouldBe("Todo");
        columns[1].GetAttribute("data-status").ShouldBe("InProgress");
        columns[2].GetAttribute("data-status").ShouldBe("Done");

        cut.Markup.ShouldContain("To do");
        cut.Markup.ShouldContain("In progress");
        cut.Markup.ShouldContain("Done");

        cut.FindAll(".board-column")[0].TextContent.ShouldContain("Write the case study");
        cut.FindAll(".board-column")[1].TextContent.ShouldContain("Review the pull request");
        cut.FindAll(".board-column")[2].TextContent.ShouldContain("Ship the release");

        cut.Markup.ShouldContain("Portfolio");
        cut.Markup.ShouldContain("High");
        cut.Markup.ShouldContain("urgent");
        cut.Markup.ShouldContain("1/3");
    }

    [Fact]
    public void KanbanBoard_DueDateWithTime_RendersDateAndTimeSeparated()
    {
        var cut = Render<KanbanBoard>(parameters => parameters
            .Add(component => component.Today, Today)
            .Add(component => component.Board, BoardWith(
                [CreateTask("Call the client", dueDate: Today, dueTime: new TimeOnly(18, 0))],
                [],
                [])));

        cut.Markup.ShouldContain("11/9/2026 6:00 PM");
    }

    [Fact]
    public void KanbanBoard_EmptyColumn_RendersPlaceholder()
    {
        var cut = Render<KanbanBoard>(parameters => parameters
            .Add(component => component.Board, BoardWith(
                [CreateTask("Only card")],
                [],
                [CreateTask("Done card", TodoTaskStatus.Done)])));

        cut.FindAll(".board-empty").ShouldHaveSingleItem().TextContent.ShouldContain("No tasks here.");
    }

    [Fact]
    public void KanbanBoard_MoveDown_InvokesMoveWithNextIndex()
    {
        var first = CreateTask("First");
        var second = CreateTask("Second");
        TaskMoveArgs? moved = null;

        var cut = Render<KanbanBoard>(parameters => parameters
            .Add(component => component.Board, BoardWith([first, second], [], []))
            .Add(component => component.OnMove, value => moved = value));

        var card = cut.Find($"[data-task-id='{first.Id}']");
        var moveDown = card.QuerySelectorAll("button")
            .First(button => button.TextContent.Trim() == "Move down");

        moveDown.Click();

        moved.ShouldNotBeNull();
        moved.TaskId.ShouldBe(first.Id);
        moved.Status.ShouldBe(TodoTaskStatus.Todo);
        moved.TargetIndex.ShouldBe(1);
    }

    [Fact]
    public void KanbanBoard_MoveToColumn_InvokesMoveAtTheEndOfTheTargetColumn()
    {
        var task = CreateTask("Move me");
        var existing = CreateTask("Already done", TodoTaskStatus.Done);
        TaskMoveArgs? moved = null;

        var cut = Render<KanbanBoard>(parameters => parameters
            .Add(component => component.Board, BoardWith([task], [], [existing]))
            .Add(component => component.OnMove, value => moved = value));

        var card = cut.Find($"[data-task-id='{task.Id}']");
        var moveToDone = card.QuerySelectorAll("button")
            .First(button => button.TextContent.Trim() == "Move to \"Done\"");

        moveToDone.Click();

        moved.ShouldNotBeNull();
        moved.TaskId.ShouldBe(task.Id);
        moved.Status.ShouldBe(TodoTaskStatus.Done);
        moved.TargetIndex.ShouldBe(1);
    }

    [Fact]
    public void KanbanBoard_DropOnAnotherColumn_InvokesMoveAtTheEnd()
    {
        var task = CreateTask("Draggable");
        var existing = CreateTask("Already done", TodoTaskStatus.Done);
        TaskMoveArgs? moved = null;

        var cut = Render<KanbanBoard>(parameters => parameters
            .Add(component => component.Board, BoardWith([task], [], [existing]))
            .Add(component => component.OnMove, value => moved = value));

        cut.Find($"[data-task-id='{task.Id}']")
            .TriggerEvent("ondragstart", new DragEventArgs());

        cut.Find(".board-column[data-status='Done'] .board-column-body")
            .TriggerEvent("ondrop", new DragEventArgs());

        moved.ShouldNotBeNull();
        moved.TaskId.ShouldBe(task.Id);
        moved.Status.ShouldBe(TodoTaskStatus.Done);
        moved.TargetIndex.ShouldBe(1);
    }

    [Fact]
    public void KanbanBoard_EditAndDelete_InvokeCallbacks()
    {
        var task = CreateTask("Edit me");
        TaskListItem? edited = null;
        TaskListItem? deleted = null;

        var cut = Render<KanbanBoard>(parameters => parameters
            .Add(component => component.Board, BoardWith([task], [], []))
            .Add(component => component.OnEdit, value => edited = value)
            .Add(component => component.OnDelete, value => deleted = value));

        var card = cut.Find($"[data-task-id='{task.Id}']");

        card.QuerySelector("button[aria-label='Edit']")!.Click();
        card.QuerySelector("button[aria-label='Delete']")!.Click();

        edited.ShouldNotBeNull();
        edited.Id.ShouldBe(task.Id);
        deleted.ShouldNotBeNull();
        deleted.Id.ShouldBe(task.Id);
    }

    private static TaskBoard BoardWith(
        TaskListItem[] todo,
        TaskListItem[] inProgress,
        TaskListItem[] done)
        => new(
        [
            new TaskBoardColumn(TodoTaskStatus.Todo, todo),
            new TaskBoardColumn(TodoTaskStatus.InProgress, inProgress),
            new TaskBoardColumn(TodoTaskStatus.Done, done)
        ]);

    private static TaskListItem CreateTask(
        string title,
        TodoTaskStatus status = TodoTaskStatus.Todo,
        TaskPriority priority = TaskPriority.None,
        DateOnly? dueDate = null,
        TimeOnly? dueTime = null,
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
            dueTime,
            projectName is null ? null : Guid.NewGuid(),
            projectName,
            tags ?? [],
            checklistTotal,
            checklistCompleted);
}
