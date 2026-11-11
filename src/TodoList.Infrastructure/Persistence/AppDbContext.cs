using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TodoList.Application.Persistence;
using TodoList.Domain.Projects;
using TodoList.Domain.Reminders;
using TodoList.Domain.Tags;
using TodoList.Domain.Tasks;
using TodoList.Infrastructure.Identity;

namespace TodoList.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<TodoTask> Tasks => Set<TodoTask>();

    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();

    public DbSet<TaskTag> TaskTags => Set<TaskTag>();

    public DbSet<Reminder> Reminders => Set<Reminder>();

    public uint GetVersion<TEntity>(TEntity entity)
        where TEntity : class
        => Entry(entity).Property<uint>("xmin").CurrentValue;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
