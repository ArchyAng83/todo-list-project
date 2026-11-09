using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TodoList.Application.Common;
using TodoList.Application.Email;
using TodoList.Application.Tags;
using TodoList.Application.Tasks;
using TodoList.Domain.Reminders;
using TodoList.Domain.Tasks;
using TodoList.Infrastructure.Identity;
using TodoList.Infrastructure.Persistence;
using TodoList.Infrastructure.Reminders;
using TodoList.Testing;

namespace TodoList.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class ReminderAndRecurrenceTests(TodoListAppFixture fixture)
{
    private static readonly DateOnly DueDate = new(2026, 11, 20);
    private static readonly TimeOnly DueTime = new(9, 0);

    [Fact]
    public async Task TaskService_CompleteRecurringTask_CreatesNextOccurrenceWithTagsAndChecklist()
    {
        var userId = await CreateUserAsync();
        var user = new UserContext(userId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        var taskQueries = scope.ServiceProvider.GetRequiredService<TaskQueries>();
        var tagService = scope.ServiceProvider.GetRequiredService<TagService>();

        var tagId = (await tagService.CreateAsync(user, new CreateTagRequest("weekly"))).Value;
        var taskId = await CreateRecurringTaskAsync(taskService, user);

        (await taskService.AddTagAsync(user, taskId, new AddTaskTagRequest(tagId)))
            .IsSuccess.ShouldBeTrue();

        var itemId = (await taskService.AddChecklistItemAsync(
            user,
            taskId,
            new AddChecklistItemRequest("Draft the update"))).Value;

        (await taskService.SetChecklistItemCompletedAsync(
            user,
            taskId,
            itemId,
            new SetChecklistItemCompletedRequest(true))).IsSuccess.ShouldBeTrue();

        (await taskService.SetStatusAsync(user, taskId, TodoTaskStatus.Done))
            .IsSuccess.ShouldBeTrue();

        var tasks = await taskQueries.ListAsync(user);
        tasks.Count.ShouldBe(2);

        var next = tasks.Single(task => task.Id != taskId);
        next.Status.ShouldBe(TodoTaskStatus.Todo);
        next.Title.ShouldBe("Weekly report");
        next.DueDate.ShouldBe(DueDate.AddDays(7));
        next.DueTime.ShouldBe(DueTime);
        next.Recurrence.ShouldBe(RecurrenceFrequency.Weekly);
        next.Tags.ShouldHaveSingleItem().Id.ShouldBe(tagId);
        next.ChecklistTotal.ShouldBe(1);
        next.ChecklistCompleted.ShouldBe(0);

        var details = await taskQueries.GetAsync(user, next.Id);
        details.ShouldNotBeNull();
        details.RecurrenceInterval.ShouldBe(1);
        details.RecurrenceEndDate.ShouldBe(new DateOnly(2026, 12, 31));
        details.ReminderOffsetMinutes.ShouldBe(15);
        details.Priority.ShouldBe(TaskPriority.Medium);
        details.Notes.ShouldBe("Summarize the week.");
        details.ChecklistItems.ShouldHaveSingleItem().IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task TaskService_CompleteRecurringTask_ReopenAndCompleteAgain_DoesNotDuplicate()
    {
        var userId = await CreateUserAsync();
        var user = new UserContext(userId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        var taskQueries = scope.ServiceProvider.GetRequiredService<TaskQueries>();

        var taskId = await CreateRecurringTaskAsync(taskService, user);

        await taskService.SetStatusAsync(user, taskId, TodoTaskStatus.Done);
        await taskService.SetStatusAsync(user, taskId, TodoTaskStatus.Todo);
        await taskService.SetStatusAsync(user, taskId, TodoTaskStatus.Done);

        var tasks = await taskQueries.ListAsync(user);

        tasks.Count(task => task.Title == "Weekly report").ShouldBe(2);
    }

    [Fact]
    public async Task TaskService_CompleteRecurringTask_PastEndDate_DoesNotCreateNextOccurrence()
    {
        var userId = await CreateUserAsync();
        var user = new UserContext(userId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        var taskQueries = scope.ServiceProvider.GetRequiredService<TaskQueries>();

        var taskId = (await taskService.CreateAsync(user, new CreateTaskRequest(
            "Weekly report",
            null,
            TaskPriority.None,
            DueDate,
            DueTime,
            15,
            RecurrenceFrequency.Weekly,
            1,
            DueDate.AddDays(3)))).Value;

        await taskService.SetStatusAsync(user, taskId, TodoTaskStatus.Done);

        (await taskQueries.ListAsync(user)).ShouldHaveSingleItem().Id.ShouldBe(taskId);
    }

    [Fact]
    public async Task TaskService_MoveRecurringTaskToDone_CreatesNextOccurrence()
    {
        var userId = await CreateUserAsync();
        var user = new UserContext(userId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        var taskQueries = scope.ServiceProvider.GetRequiredService<TaskQueries>();

        var taskId = await CreateRecurringTaskAsync(taskService, user);

        (await taskService.MoveAsync(user, taskId, new MoveTaskRequest(TodoTaskStatus.Done, 0)))
            .IsSuccess.ShouldBeTrue();

        var tasks = await taskQueries.ListAsync(user);

        tasks.Count.ShouldBe(2);
        tasks.ShouldContain(task => task.DueDate == DueDate.AddDays(7));
    }

    [Fact]
    public async Task TaskReminderScheduler_UpdateReschedulesPendingReminder()
    {
        var userId = await CreateUserAsync();
        var user = new UserContext(userId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var taskId = await CreateRecurringTaskAsync(taskService, user);

        var initial = await db.Reminders
            .AsNoTracking()
            .SingleAsync(reminder => reminder.TaskId == taskId);

        initial.Status.ShouldBe(ReminderStatus.Pending);
        initial.ScheduledAtUtc.ShouldBe(new DateTimeOffset(
            DueDate.ToDateTime(DueTime),
            TimeSpan.Zero).AddMinutes(-15));

        (await taskService.UpdateAsync(
            user,
            taskId,
            new UpdateTaskRequest(
                "Weekly report",
                "Summarize the week.",
                null,
                TaskPriority.Medium,
                DueDate,
                new TimeOnly(10, 30),
                30,
                RecurrenceFrequency.Weekly,
                1,
                new DateOnly(2026, 12, 31)))).IsSuccess.ShouldBeTrue();

        var reminders = await db.Reminders
            .AsNoTracking()
            .Where(reminder => reminder.TaskId == taskId)
            .ToListAsync();

        reminders.Count.ShouldBe(2);
        reminders.ShouldContain(reminder => reminder.Status == ReminderStatus.Cancelled);

        var pending = reminders
            .Where(reminder => reminder.Status == ReminderStatus.Pending)
            .ToList();

        pending.ShouldHaveSingleItem().ScheduledAtUtc.ShouldBe(new DateTimeOffset(
            DueDate.ToDateTime(new TimeOnly(10, 30)),
            TimeSpan.Zero).AddMinutes(-30));
    }

    [Fact]
    public async Task TaskReminderScheduler_CompleteTask_CancelsPendingReminder()
    {
        var userId = await CreateUserAsync();
        var user = new UserContext(userId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var taskId = await CreateRecurringTaskAsync(taskService, user);

        await taskService.SetStatusAsync(user, taskId, TodoTaskStatus.Done);

        var reminders = await db.Reminders
            .AsNoTracking()
            .Where(reminder => reminder.TaskId == taskId)
            .ToListAsync();

        reminders.ShouldHaveSingleItem().Status.ShouldBe(ReminderStatus.Cancelled);
    }

    [Fact]
    public async Task ReminderDeliveryService_DueReminder_SendsOnceAndMarksSent()
    {
        var userId = await CreateUserAsync();
        var user = new UserContext(userId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var taskId = (await taskService.CreateAsync(user, new CreateTaskRequest(
            "Send the report",
            null,
            TaskPriority.None,
            DueDate,
            DueTime))).Value;

        var now = DateTimeOffset.UtcNow;
        var reminder = Reminder.Create(userId, taskId, now.AddMinutes(-1), now);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var sender = new CapturingEmailSender();
        var delivery = ActivatorUtilities.CreateInstance<ReminderDeliveryService>(
            scope.ServiceProvider,
            sender);

        var sent = await delivery.DispatchDueAsync();

        sent.ShouldBe(1);
        sender.Messages.ShouldHaveSingleItem().Subject.ShouldContain("Send the report");

        var stored = await db.Reminders
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == reminder.Id);

        stored.Status.ShouldBe(ReminderStatus.Sent);
        stored.SentAtUtc.ShouldNotBeNull();

        (await delivery.DispatchDueAsync()).ShouldBe(0);
        sender.Messages.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ReminderDeliveryService_UsesRecipientCulture()
    {
        var userId = await CreateUserAsync("ru");
        var user = new UserContext(userId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var taskId = (await taskService.CreateAsync(user, new CreateTaskRequest(
            "Подготовить отчёт",
            null,
            TaskPriority.None,
            DueDate,
            DueTime))).Value;

        var now = DateTimeOffset.UtcNow;
        db.Reminders.Add(Reminder.Create(userId, taskId, now.AddMinutes(-1), now));
        await db.SaveChangesAsync();

        var sender = new CapturingEmailSender();
        var delivery = ActivatorUtilities.CreateInstance<ReminderDeliveryService>(
            scope.ServiceProvider,
            sender);

        (await delivery.DispatchDueAsync()).ShouldBe(1);

        var message = sender.Messages.ShouldHaveSingleItem();
        message.Subject.ShouldStartWith("Напоминание:");
        message.HtmlBody.ShouldContain("Подготовить отчёт");
    }

    [Fact]
    public async Task ReminderDeliveryService_CompletedTask_CancelsWithoutSending()
    {
        var userId = await CreateUserAsync();
        var user = new UserContext(userId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var taskId = (await taskService.CreateAsync(user, new CreateTaskRequest(
            "Already done",
            null,
            TaskPriority.None,
            DueDate,
            DueTime))).Value;

        var now = DateTimeOffset.UtcNow;
        var reminder = Reminder.Create(userId, taskId, now.AddMinutes(-1), now);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        await taskService.SetStatusAsync(user, taskId, TodoTaskStatus.Done);

        var sender = new CapturingEmailSender();
        var delivery = ActivatorUtilities.CreateInstance<ReminderDeliveryService>(
            scope.ServiceProvider,
            sender);

        (await delivery.DispatchDueAsync()).ShouldBe(0);
        sender.Messages.ShouldBeEmpty();

        var stored = await db.Reminders
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == reminder.Id);

        stored.Status.ShouldBe(ReminderStatus.Cancelled);
    }

    private static async Task<Guid> CreateRecurringTaskAsync(TaskService taskService, UserContext user)
    {
        var taskId = (await taskService.CreateAsync(user, new CreateTaskRequest(
            "Weekly report",
            null,
            TaskPriority.Medium,
            DueDate,
            DueTime,
            15,
            RecurrenceFrequency.Weekly,
            1,
            new DateOnly(2026, 12, 31)))).Value;

        (await taskService.UpdateAsync(
            user,
            taskId,
            new UpdateTaskRequest(
                "Weekly report",
                "Summarize the week.",
                null,
                TaskPriority.Medium,
                DueDate,
                DueTime,
                15,
                RecurrenceFrequency.Weekly,
                1,
                new DateOnly(2026, 12, 31)))).IsSuccess.ShouldBeTrue();

        return taskId;
    }

    private async Task<Guid> CreateUserAsync(string culture = "en")
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var email = $"reminder-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            DisplayName = "Reminder User",
            Culture = culture,
            TimeZoneId = "UTC",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, "Password1");
        result.Succeeded.ShouldBeTrue(
            string.Join(", ", result.Errors.Select(error => error.Description)));

        return user.Id;
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
