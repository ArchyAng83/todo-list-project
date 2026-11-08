using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TodoList.Domain.Projects;
using TodoList.Domain.Tags;
using TodoList.Domain.Tasks;
using TodoList.Infrastructure.Identity;
using TodoList.Infrastructure.Persistence;
using TodoList.Testing;

namespace TodoList.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class DatabaseTests(TodoListAppFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Migrations_AreApplied()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var migrations = await db.Database.GetAppliedMigrationsAsync();

        migrations.ShouldContain(migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DomainTables_Exist()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            select count(*)
            from information_schema.tables
            where table_schema = 'public'
              and table_name in ('users', 'projects', 'tags', 'tasks', 'checklist_items', 'reminders', 'task_tags')
            """;

        var count = (long)(await command.ExecuteScalarAsync())!;

        count.ShouldBe(7L);
    }

    [Fact]
    public async Task TaskAggregate_RoundTripsThroughPostgres()
    {
        var userId = await CreateUserAsync();

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var project = Project.Create(userId, "Inbox", Now);
            var tag = Tag.Create(userId, "deep-work", Now);
            var task = TodoTask.Create(userId, "Write integration tests", Now);
            task.MoveToProject(project.Id, Now);
            task.SetPriority(TaskPriority.High, Now);
            task.SetDueDate(new DateOnly(2026, 11, 10), new TimeOnly(9, 0), Now);
            task.SetReminder(30, Now);
            task.AddChecklistItem("Cover EF mappings", Now);
            task.AddTag(tag, Now);

            db.Projects.Add(project);
            db.Tags.Add(tag);
            db.Tasks.Add(task);

            await db.SaveChangesAsync();
        }

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var task = await db.Tasks
                .Include(x => x.ChecklistItems)
                .Include(x => x.Tags)
                .SingleAsync(x => x.UserId == userId);

            task.Title.ShouldBe("Write integration tests");
            task.Status.ShouldBe(TodoTaskStatus.Todo);
            task.Priority.ShouldBe(TaskPriority.High);
            task.DueDate.ShouldBe(new DateOnly(2026, 11, 10));
            task.DueTime.ShouldBe(new TimeOnly(9, 0));
            task.ReminderOffsetMinutes.ShouldBe(30);
            task.ChecklistItems.Single().Title.ShouldBe("Cover EF mappings");
            task.Tags.Single().TagId.ShouldNotBe(Guid.Empty);
        }
    }

    private async Task<Guid> CreateUserAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var email = $"db-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            DisplayName = "DB User",
            Culture = "en",
            CreatedAt = Now
        };

        var result = await userManager.CreateAsync(user, "Password1");
        result.Succeeded.ShouldBeTrue(
            string.Join(", ", result.Errors.Select(error => error.Description)));

        return user.Id;
    }
}
