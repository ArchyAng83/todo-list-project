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
    public void MarkFailed_FirstFailure_KeepsPendingAndSchedulesRetry()
    {
        var reminder = Reminder.Create(Guid.NewGuid(), Guid.NewGuid(), Now, Now);

        reminder.MarkFailed("smtp unavailable", Now);

        reminder.Status.ShouldBe(ReminderStatus.Pending);
        reminder.Attempts.ShouldBe(1);
        reminder.LastError.ShouldBe("smtp unavailable");
        reminder.ScheduledAtUtc.ShouldBe(Now.AddMinutes(1));
    }

    [Fact]
    public void MarkFailed_RepeatedFailures_BackOffExponentially()
    {
        var reminder = Reminder.Create(Guid.NewGuid(), Guid.NewGuid(), Now, Now);

        reminder.MarkFailed("first", Now);
        reminder.ScheduledAtUtc.ShouldBe(Now.AddMinutes(1));

        reminder.MarkFailed("second", Now);
        reminder.ScheduledAtUtc.ShouldBe(Now.AddMinutes(2));

        reminder.MarkFailed("third", Now);
        reminder.ScheduledAtUtc.ShouldBe(Now.AddMinutes(4));

        reminder.MarkFailed("fourth", Now);

        reminder.Attempts.ShouldBe(4);
        reminder.Status.ShouldBe(ReminderStatus.Pending);
        reminder.ScheduledAtUtc.ShouldBe(Now.AddMinutes(8));
    }

    [Fact]
    public void MarkFailed_AfterMaxAttempts_BecomesTerminal()
    {
        var reminder = Reminder.Create(Guid.NewGuid(), Guid.NewGuid(), Now, Now);

        for (var attempt = 1; attempt <= Reminder.MaxAttempts; attempt++)
        {
            reminder.MarkFailed($"failure {attempt}", Now);
        }

        reminder.Status.ShouldBe(ReminderStatus.Failed);
        reminder.Attempts.ShouldBe(Reminder.MaxAttempts);
        reminder.LastError.ShouldBe($"failure {Reminder.MaxAttempts}");
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
