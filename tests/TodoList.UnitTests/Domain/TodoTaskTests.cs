using Shouldly;
using TodoList.Domain.Projects;
using TodoList.Domain.Tags;
using TodoList.Domain.Tasks;

namespace TodoList.UnitTests.Domain;

public sealed class TodoTaskTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidTitle_TrimsTitleAndSetsDefaults()
    {
        var task = TodoTask.Create(UserId, "  Write tests  ", Now);

        task.Title.ShouldBe("Write tests");
        task.Status.ShouldBe(TodoTaskStatus.Todo);
        task.Priority.ShouldBe(TaskPriority.None);
        task.UserId.ShouldBe(UserId);
        task.CreatedAt.ShouldBe(Now);
        task.UpdatedAt.ShouldBe(Now);
        task.CompletedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankTitle_Throws(string title)
    {
        Should.Throw<ArgumentException>(() => TodoTask.Create(UserId, title, Now));
    }

    [Fact]
    public void Create_WithTooLongTitle_Throws()
    {
        var title = new string('a', TodoTask.TitleMaxLength + 1);

        Should.Throw<ArgumentException>(() => TodoTask.Create(UserId, title, Now));
    }

    [Fact]
    public void Create_WithEmptyUserId_Throws()
    {
        Should.Throw<ArgumentException>(() => TodoTask.Create(Guid.Empty, "Task", Now));
    }

    [Fact]
    public void SetDueDate_WithTimeButNoDate_Throws()
    {
        var task = TodoTask.Create(UserId, "Task", Now);

        Should.Throw<ArgumentException>(() => task.SetDueDate(null, new TimeOnly(9, 0), Now));
    }

    [Fact]
    public void SetDueDate_ClearingDate_ClearsReminder()
    {
        var task = TodoTask.Create(UserId, "Task", Now);
        task.SetDueDate(new DateOnly(2026, 11, 10), new TimeOnly(9, 0), Now);
        task.SetReminder(30, Now);

        task.SetDueDate(null, null, Now);

        task.DueDate.ShouldBeNull();
        task.DueTime.ShouldBeNull();
        task.ReminderOffsetMinutes.ShouldBeNull();
    }

    [Fact]
    public void SetReminder_WithoutDueDate_Throws()
    {
        var task = TodoTask.Create(UserId, "Task", Now);

        Should.Throw<InvalidOperationException>(() => task.SetReminder(15, Now));
    }

    [Fact]
    public void SetReminder_WithOutOfRangeOffset_Throws()
    {
        var task = TodoTask.Create(UserId, "Task", Now);
        task.SetDueDate(new DateOnly(2026, 11, 10), null, Now);

        Should.Throw<ArgumentOutOfRangeException>(() => task.SetReminder(-1, Now));
        Should.Throw<ArgumentOutOfRangeException>(
            () => task.SetReminder(TodoTask.MaxReminderOffsetMinutes + 1, Now));
    }

    [Fact]
    public void SetStatus_Done_SetsCompletedAt()
    {
        var task = TodoTask.Create(UserId, "Task", Now);

        task.SetStatus(TodoTaskStatus.Done, Now);

        task.Status.ShouldBe(TodoTaskStatus.Done);
        task.CompletedAt.ShouldBe(Now);
    }

    [Fact]
    public void SetStatus_Reopening_ClearsCompletedAt()
    {
        var task = TodoTask.Create(UserId, "Task", Now);
        task.SetStatus(TodoTaskStatus.Done, Now);

        task.SetStatus(TodoTaskStatus.Todo, Now);

        task.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public void AddChecklistItem_AssignsIncrementalSortOrder()
    {
        var task = TodoTask.Create(UserId, "Task", Now);

        var first = task.AddChecklistItem("First", Now);
        var second = task.AddChecklistItem("Second", Now);

        first.SortOrder.ShouldBe(0);
        second.SortOrder.ShouldBe(1);
        task.ChecklistItems.Count.ShouldBe(2);
    }

    [Fact]
    public void AddTag_WithTagOwnedByAnotherUser_Throws()
    {
        var task = TodoTask.Create(UserId, "Task", Now);
        var foreignTag = Tag.Create(Guid.NewGuid(), "foreign", Now);

        Should.Throw<InvalidOperationException>(() => task.AddTag(foreignTag, Now));
    }

    [Fact]
    public void AddTag_AddsLinkOnlyOnce()
    {
        var task = TodoTask.Create(UserId, "Task", Now);
        var tag = Tag.Create(UserId, "work", Now);

        task.AddTag(tag, Now);
        task.AddTag(tag, Now);

        task.Tags.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveTag_RemovesLink()
    {
        var task = TodoTask.Create(UserId, "Task", Now);
        var tag = Tag.Create(UserId, "work", Now);
        task.AddTag(tag, Now);

        task.RemoveTag(tag.Id, Now);

        task.Tags.ShouldBeEmpty();
    }
}
