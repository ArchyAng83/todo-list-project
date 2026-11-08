using Shouldly;
using TodoList.Domain.Reminders;

namespace TodoList.UnitTests.Domain;

public sealed class ReminderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_StartsPending()
    {
        var reminder = Reminder.Create(Guid.NewGuid(), Guid.NewGuid(), Now.AddHours(1), Now);

        reminder.Status.ShouldBe(ReminderStatus.Pending);
        reminder.Attempts.ShouldBe(0);
        reminder.SentAtUtc.ShouldBeNull();
    }

    [Fact]
    public void Create_WithEmptyIds_Throws()
    {
        Should.Throw<ArgumentException>(
            () => Reminder.Create(Guid.Empty, Guid.NewGuid(), Now, Now));

        Should.Throw<ArgumentException>(
            () => Reminder.Create(Guid.NewGuid(), Guid.Empty, Now, Now));
    }

    [Fact]
    public void MarkSent_SetsStatusAndTimestamp()
    {
        var reminder = Reminder.Create(Guid.NewGuid(), Guid.NewGuid(), Now, Now);
        var sentAt = Now.AddMinutes(5);

        reminder.MarkSent(sentAt);

        reminder.Status.ShouldBe(ReminderStatus.Sent);
        reminder.SentAtUtc.ShouldBe(sentAt);
    }

    [Fact]
    public void MarkFailed_IncrementsAttemptsAndStoresError()
    {
        var reminder = Reminder.Create(Guid.NewGuid(), Guid.NewGuid(), Now, Now);

        reminder.MarkFailed("smtp unavailable");

        reminder.Status.ShouldBe(ReminderStatus.Failed);
        reminder.Attempts.ShouldBe(1);
        reminder.LastError.ShouldBe("smtp unavailable");
    }

    [Fact]
    public void Cancel_AfterSent_KeepsSentStatus()
    {
        var reminder = Reminder.Create(Guid.NewGuid(), Guid.NewGuid(), Now, Now);
        reminder.MarkSent(Now);

        reminder.Cancel();

        reminder.Status.ShouldBe(ReminderStatus.Sent);
    }
}
