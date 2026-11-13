using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using TodoList.Application.Common;
using TodoList.Application.Tasks;
using TodoList.Domain.Reminders;
using TodoList.Domain.Tasks;
using TodoList.Infrastructure.Identity;
using TodoList.Infrastructure.Persistence;
using TodoList.Infrastructure.Reminders;
using TodoList.Testing;

namespace TodoList.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class ReminderDispatchTests(TodoListAppFixture fixture)
{
    [Fact]
    public async Task ReminderDispatchLock_SecondScope_CannotAcquireWhileFirstHoldsIt()
    {
        await using var firstScope = fixture.Factory.Services.CreateAsyncScope();
        await using var secondScope = fixture.Factory.Services.CreateAsyncScope();

        var first = firstScope.ServiceProvider.GetRequiredService<ReminderDispatchLock>();
        var second = secondScope.ServiceProvider.GetRequiredService<ReminderDispatchLock>();

        (await first.TryAcquireAsync()).ShouldBeTrue();
        (await second.TryAcquireAsync()).ShouldBeFalse();

        await first.DisposeAsync();

        (await second.TryAcquireAsync()).ShouldBeTrue();
        await second.DisposeAsync();
    }

    [Fact]
    public async Task ReminderDispatchLock_RepeatedAcquire_IsIdempotentAndReleasesOnce()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dispatchLock = scope.ServiceProvider.GetRequiredService<ReminderDispatchLock>();

        (await dispatchLock.TryAcquireAsync()).ShouldBeTrue();
        (await dispatchLock.TryAcquireAsync()).ShouldBeTrue();

        await dispatchLock.DisposeAsync();
        await dispatchLock.DisposeAsync();

        await using var secondScope = fixture.Factory.Services.CreateAsyncScope();
        var second = secondScope.ServiceProvider.GetRequiredService<ReminderDispatchLock>();

        (await second.TryAcquireAsync()).ShouldBeTrue();
        await second.DisposeAsync();
    }

    [Fact]
    public async Task ReminderRetentionService_TerminalRemindersOlderThanRetention_AreDeleted()
    {
        var userId = await CreateUserAsync();
        var user = new UserContext(userId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var taskId = (await taskService.CreateAsync(user, new CreateTaskRequest(
            "Retention fixture",
            null,
            TaskPriority.None,
            new DateOnly(2026, 11, 20),
            new TimeOnly(9, 0)))).Value;

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero));
        var now = clock.GetUtcNow();
        var old = now.AddDays(-31);

        var oldSent = Reminder.Create(userId, taskId, old.AddMinutes(-5), old);
        oldSent.MarkSent(old);

        var oldFailed = Reminder.Create(userId, taskId, old, old);
        for (var attempt = 1; attempt <= Reminder.MaxAttempts; attempt++)
        {
            oldFailed.MarkFailed("SMTP unavailable", old);
        }

        var oldCancelled = Reminder.Create(userId, taskId, old, old);
        oldCancelled.Cancel();

        var oldPending = Reminder.Create(userId, taskId, old, old);

        var recentSent = Reminder.Create(userId, taskId, now.AddDays(-1), now.AddDays(-1));
        recentSent.MarkSent(now.AddDays(-1));

        db.Reminders.AddRange(oldSent, oldFailed, oldCancelled, oldPending, recentSent);
        await db.SaveChangesAsync();

        var retention = ActivatorUtilities.CreateInstance<ReminderRetentionService>(
            scope.ServiceProvider,
            clock);

        (await retention.DeleteExpiredAsync()).ShouldBe(3);

        var remaining = await db.Reminders
            .AsNoTracking()
            .Where(reminder => reminder.Id == oldSent.Id
                || reminder.Id == oldFailed.Id
                || reminder.Id == oldCancelled.Id
                || reminder.Id == oldPending.Id
                || reminder.Id == recentSent.Id)
            .Select(reminder => reminder.Id)
            .ToListAsync();

        remaining.Count.ShouldBe(2);
        remaining.ShouldContain(oldPending.Id);
        remaining.ShouldContain(recentSent.Id);
    }

    private async Task<Guid> CreateUserAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var email = $"retention-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            DisplayName = "Retention User",
            Culture = "en",
            TimeZoneId = "UTC",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, "Password1");
        result.Succeeded.ShouldBeTrue(
            string.Join(", ", result.Errors.Select(error => error.Description)));

        return user.Id;
    }
}
