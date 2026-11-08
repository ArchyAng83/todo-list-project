using Microsoft.EntityFrameworkCore;
using TodoList.Domain.Projects;
using TodoList.Domain.Reminders;
using TodoList.Domain.Tags;
using TodoList.Domain.Tasks;

namespace TodoList.Application.Persistence;

/// <summary>
/// Thin abstraction over the EF Core context used by Application services.
/// </summary>
public interface IAppDbContext
{
    DbSet<Project> Projects { get; }

    DbSet<Tag> Tags { get; }

    DbSet<TodoTask> Tasks { get; }

    DbSet<ChecklistItem> ChecklistItems { get; }

    DbSet<TaskTag> TaskTags { get; }

    DbSet<Reminder> Reminders { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
