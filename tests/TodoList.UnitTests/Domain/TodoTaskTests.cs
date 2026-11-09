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

    [Fact]
    public void SetDueDate_ClearingDate_ClearsRecurrence()
    {
        var task = TodoTask.Create(UserId, "Task", Now);
        task.SetDueDate(new DateOnly(2026, 11, 10), null, Now);
        task.SetRecurrence(RecurrenceFrequency.Weekly, 2, new DateOnly(2026, 12, 31), Now);

        task.SetDueDate(null, null, Now);

        task.Recurrence.ShouldBe(RecurrenceFrequency.None);
        task.RecurrenceInterval.ShouldBe(1);
        task.RecurrenceEndDate.ShouldBeNull();
        task.RecurrenceGenerated.ShouldBeFalse();
    }

    [Fact]
    public void SetRecurrence_WithoutDueDate_Throws()
    {
        var task = TodoTask.Create(UserId, "Task", Now);

        Should.Throw<InvalidOperationException>(
            () => task.SetRecurrence(RecurrenceFrequency.Daily, 1, null, Now));
    }

    [Fact]
    public void SetRecurrence_WithOutOfRangeInterval_Throws()
    {
        var task = TodoTask.Create(UserId, "Task", Now);
        task.SetDueDate(new DateOnly(2026, 11, 10), null, Now);

        Should.Throw<ArgumentOutOfRangeException>(
            () => task.SetRecurrence(RecurrenceFrequency.Daily, 0, null, Now));

        Should.Throw<ArgumentOutOfRangeException>(
            () => task.SetRecurrence(
                RecurrenceFrequency.Daily,
                TodoTask.MaxRecurrenceInterval + 1,
                null,
                Now));
    }

    [Fact]
    public void SetRecurrence_WithEndDateBeforeDueDate_Throws()
    {
        var task = TodoTask.Create(UserId, "Task", Now);
        task.SetDueDate(new DateOnly(2026, 11, 10), null, Now);

        Should.Throw<ArgumentException>(
            () => task.SetRecurrence(
                RecurrenceFrequency.Weekly,
                1,
                new DateOnly(2026, 11, 9),
                Now));
    }

    [Fact]
    public void CreateNextOccurrence_NonRecurringTask_ReturnsNull()
    {
        var task = TodoTask.Create(UserId, "Task", Now);
        task.SetDueDate(new DateOnly(2026, 11, 10), null, Now);

        task.CreateNextOccurrence(Now).ShouldBeNull();
    }

    [Fact]
    public void CreateNextOccurrence_Daily_AddsIntervalDays()
    {
        var task = CreateRecurringTask(RecurrenceFrequency.Daily, interval: 2);

        var next = task.CreateNextOccurrence(Now);

        next.ShouldNotBeNull();
        next.DueDate.ShouldBe(new DateOnly(2026, 11, 12));
        next.Status.ShouldBe(TodoTaskStatus.Todo);
        next.Recurrence.ShouldBe(RecurrenceFrequency.Daily);
        next.RecurrenceInterval.ShouldBe(2);
    }

    [Fact]
    public void CreateNextOccurrence_Weekly_AddsSevenDaysPerInterval()
    {
        var task = CreateRecurringTask(RecurrenceFrequency.Weekly, interval: 3);

        var next = task.CreateNextOccurrence(Now);

        next.ShouldNotBeNull();
        next.DueDate.ShouldBe(new DateOnly(2026, 12, 1));
    }

    [Fact]
    public void CreateNextOccurrence_Monthly_AddsCalendarMonths()
    {
        var task = CreateRecurringTask(RecurrenceFrequency.Monthly, interval: 1);

        var next = task.CreateNextOccurrence(Now);

        next.ShouldNotBeNull();
        next.DueDate.ShouldBe(new DateOnly(2026, 12, 10));
    }

    [Fact]
    public void CreateNextOccurrence_CopiesScheduleAndNotes()
    {
        var task = CreateRecurringTask(RecurrenceFrequency.Weekly, interval: 1);
        task.Rename("Title", Now);
        task.SetNotes("Some notes", Now);
        task.SetPriority(TaskPriority.High, Now);

        var next = task.CreateNextOccurrence(Now);

        next.ShouldNotBeNull();
        next.UserId.ShouldBe(task.UserId);
        next.Title.ShouldBe("Title");
        next.Notes.ShouldBe("Some notes");
        next.Priority.ShouldBe(TaskPriority.High);
        next.DueTime.ShouldBe(new TimeOnly(9, 30));
        next.ReminderOffsetMinutes.ShouldBe(15);
        next.RecurrenceEndDate.ShouldBe(task.RecurrenceEndDate);
    }

    [Fact]
    public void CreateNextOccurrence_AlreadyGenerated_ReturnsNull()
    {
        var task = CreateRecurringTask(RecurrenceFrequency.Daily, interval: 1);

        var first = task.CreateNextOccurrence(Now);
        first.ShouldNotBeNull();

        task.CreateNextOccurrence(Now).ShouldBeNull();
    }

    [Fact]
    public void CreateNextOccurrence_PastEndDate_ReturnsNull()
    {
        var task = TodoTask.Create(UserId, "Task", Now);
        task.SetDueDate(new DateOnly(2026, 11, 10), null, Now);
        task.SetRecurrence(RecurrenceFrequency.Weekly, 1, new DateOnly(2026, 11, 15), Now);

        task.CreateNextOccurrence(Now).ShouldBeNull();
    }

    [Fact]
    public void CopyStructureFrom_CopiesTagsAndResetsChecklistItems()
    {
        var source = CreateRecurringTask(RecurrenceFrequency.Daily, interval: 1);
        var tag = Tag.Create(UserId, "work", Now);
        source.AddTag(tag, Now);
        var doneItem = source.AddChecklistItem("Done step", Now);
        doneItem.SetCompleted(true, Now);
        source.AddChecklistItem("Open step", Now);

        var next = TodoTask.Create(UserId, "Task", Now);
        next.CopyStructureFrom(source, Now);

        next.Tags.ShouldHaveSingleItem().TagId.ShouldBe(tag.Id);
        next.ChecklistItems.Count.ShouldBe(2);
        next.ChecklistItems.ShouldAllBe(item => !item.IsCompleted);
        next.ChecklistItems.Select(item => item.Title)
            .ShouldBe(["Done step", "Open step"]);
    }

    private static TodoTask CreateRecurringTask(RecurrenceFrequency frequency, int interval)
    {
        var task = TodoTask.Create(UserId, "Task", Now);
        task.SetDueDate(new DateOnly(2026, 11, 10), new TimeOnly(9, 30), Now);
        task.SetReminder(15, Now);
        task.SetRecurrence(frequency, interval, new DateOnly(2027, 1, 1), Now);
        return task;
    }
}
