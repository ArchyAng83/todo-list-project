using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TodoList.Domain.Projects;
using TodoList.Domain.Tags;
using TodoList.Domain.Tasks;
using TodoList.Infrastructure.Identity;

namespace TodoList.Infrastructure.Persistence;

/// <summary>
/// Creates a deterministic demo account with projects, tags, tasks, and
/// checklist items so the app can be evaluated without manual data entry.
/// Only used in Development.
/// </summary>
public sealed class DemoDataSeeder(
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    TimeProvider timeProvider,
    ILogger<DemoDataSeeder> logger)
{
    public const string Email = "demo@todolist.local";

    public const string Password = "DemoPass123";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await userManager.FindByEmailAsync(Email) is not null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = Email,
            Email = Email,
            EmailConfirmed = true,
            DisplayName = "Demo User",
            Culture = "en",
            TimeZoneId = "UTC",
            Theme = "system",
            WeekStart = DayOfWeek.Monday,
            DefaultReminderMinutes = 15,
            CreatedAt = now
        };

        var created = await userManager.CreateAsync(user, Password);

        if (!created.Succeeded)
        {
            logger.LogWarning(
                "Demo user could not be created: {Errors}",
                string.Join(", ", created.Errors.Select(error => error.Description)));
            return;
        }

        var portfolio = Project.Create(user.Id, "Portfolio", now);
        portfolio.SetColor("#4F46E5", now);

        var home = Project.Create(user.Id, "Home", now);
        home.SetColor("#059669", now);

        var work = Project.Create(user.Id, "Work", now);
        work.SetColor("#0EA5E9", now);

        var urgent = Tag.Create(user.Id, "urgent", now, "#EF4444");
        var deepWork = Tag.Create(user.Id, "deep work", now, "#6366F1");
        var errand = Tag.Create(user.Id, "errand", now, "#10B981");

        db.Projects.AddRange(portfolio, home, work);
        db.Tags.AddRange(urgent, deepWork, errand);

        AddTask(
            user.Id,
            "Write portfolio case study",
            today,
            new TimeOnly(18, 0),
            project: portfolio.Id,
            priority: TaskPriority.High,
            notes: "Cover the architecture, the test pyramid, and the trade-offs.",
            now: now,
            tags: [urgent],
            checklist: ["Outline the story", "Collect metrics", "Publish"]);

        AddTask(
            user.Id,
            "Polish the README",
            today.AddDays(1),
            null,
            project: portfolio.Id,
            priority: TaskPriority.Medium,
            notes: null,
            now: now,
            tags: [deepWork],
            checklist: []);

        AddTask(
            user.Id,
            "Buy groceries",
            today,
            new TimeOnly(19, 30),
            project: home.Id,
            priority: TaskPriority.Low,
            notes: "Milk, eggs, coffee.",
            now: now,
            tags: [errand],
            checklist: []);

        AddTask(
            user.Id,
            "Plan the weekly review",
            null,
            null,
            project: null,
            priority: TaskPriority.Medium,
            notes: "Decide what to carry over.",
            now: now,
            tags: [],
            checklist: ["Review open tasks", "Pick next week's goals"]);

        AddTask(
            user.Id,
            "Fix the failing CI build",
            today.AddDays(-1),
            null,
            project: portfolio.Id,
            priority: TaskPriority.Urgent,
            notes: null,
            now: now,
            tags: [urgent],
            checklist: []);

        AddTask(
            user.Id,
            "Read the ASP.NET Core docs",
            today.AddDays(3),
            null,
            project: null,
            priority: TaskPriority.Low,
            notes: null,
            now: now,
            tags: [deepWork],
            checklist: []);

        AddTask(
            user.Id,
            "Publish v0.2 release",
            today.AddDays(7),
            null,
            project: portfolio.Id,
            priority: TaskPriority.High,
            notes: null,
            now: now,
            tags: [urgent, deepWork],
            checklist: ["Update CHANGELOG", "Tag the release", "Write release notes"]);

        AddTask(
            user.Id,
            "Water the plants",
            today.AddDays(-2),
            null,
            project: home.Id,
            priority: TaskPriority.None,
            notes: null,
            now: now,
            tags: [],
            checklist: [],
            status: TodoTaskStatus.Done);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Demo data seeded for {Email}.", Email);
    }

    private void AddTask(
        Guid userId,
        string title,
        DateOnly? dueDate,
        TimeOnly? dueTime,
        Guid? project,
        TaskPriority priority,
        string? notes,
        DateTimeOffset now,
        Tag[] tags,
        string[] checklist,
        TodoTaskStatus status = TodoTaskStatus.Todo)
    {
        var task = TodoTask.Create(userId, title, now);

        task.MoveToProject(project, now);
        task.SetPriority(priority, now);
        task.SetNotes(notes, now);
        task.SetDueDate(dueDate, dueTime, now);

        if (dueDate is not null)
        {
            task.SetReminder(15, now);
        }

        foreach (var tag in tags)
        {
            task.AddTag(tag, now);
        }

        foreach (var item in checklist)
        {
            task.AddChecklistItem(item, now);
        }

        task.SetStatus(status, now);

        db.Tasks.Add(task);
    }
}
