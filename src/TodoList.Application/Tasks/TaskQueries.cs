using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TodoList.Application.Common;
using TodoList.Application.Persistence;
using TodoList.Application.Tags;
using TodoList.Domain.Tasks;

namespace TodoList.Application.Tasks;

public sealed class TaskQueries(IAppDbContext db, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<TaskListItem>> ListAsync(
        UserContext user,
        TaskListFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new TaskListFilter();

        var query = db.Tasks
            .AsNoTracking()
            .Where(task => task.UserId == user.UserId);

        query = ApplyView(query, filter.View, LocalToday(user));

        if (filter.Status is { } status)
        {
            query = query.Where(task => task.Status == status);
        }

        if (filter.ProjectId is { } projectId)
        {
            query = query.Where(task => task.ProjectId == projectId);
        }

        if (filter.TagId is { } tagId)
        {
            query = query.Where(task => task.Tags.Any(link => link.TagId == tagId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(task =>
                task.Title.ToLower().Contains(term)
                || (task.Notes != null && task.Notes.ToLower().Contains(term)));
        }

        return await ApplySort(query, filter.Sort)
            .Select(ToListItem)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Returns every task grouped into workflow columns, ordered by the manual
    /// <see cref="TodoTask.SortOrder"/> so the Kanban board can render and reorder
    /// cards predictably.
    /// </summary>
    public async Task<TaskBoard> BoardAsync(
        UserContext user,
        CancellationToken cancellationToken = default)
    {
        var tasks = await db.Tasks
            .AsNoTracking()
            .Where(task => task.UserId == user.UserId)
            .OrderBy(task => task.SortOrder)
            .ThenBy(task => task.CreatedAt)
            .Select(ToListItem)
            .ToListAsync(cancellationToken);

        var columns = Enum.GetValues<TodoTaskStatus>()
            .Select(status => new TaskBoardColumn(
                status,
                tasks.Where(task => task.Status == status).ToList()))
            .ToList();

        return new TaskBoard(columns);
    }

    /// <summary>
    /// Returns tasks with a due date inside the inclusive range, ordered by day,
    /// then time, status, and priority. The UI groups them into a month grid.
    /// </summary>
    public async Task<IReadOnlyList<TaskListItem>> CalendarAsync(
        UserContext user,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (from > to)
        {
            return [];
        }

        return await db.Tasks
            .AsNoTracking()
            .Where(task => task.UserId == user.UserId
                && task.DueDate != null
                && task.DueDate >= from
                && task.DueDate <= to)
            .OrderBy(task => task.DueDate)
            .ThenBy(task => task.DueTime.HasValue ? 0 : 1)
            .ThenBy(task => task.DueTime)
            .ThenBy(task => task.Status == TodoTaskStatus.Done)
            .ThenByDescending(task => task.Priority)
            .Select(ToListItem)
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskDetails?> GetAsync(
        UserContext user,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var task = await db.Tasks
            .AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.ChecklistItems)
            .Include(x => x.Tags)
            .ThenInclude(link => link.Tag)
            .SingleOrDefaultAsync(
                x => x.Id == taskId && x.UserId == user.UserId,
                cancellationToken);

        if (task is null)
        {
            return null;
        }

        // The xmin token lives as a shadow property, so it is read with a small
        // projection instead of being carried by the tracked entity.
        var version = await db.Tasks
            .Where(candidate => candidate.Id == taskId && candidate.UserId == user.UserId)
            .Select(candidate => EF.Property<uint>(candidate, "xmin"))
            .SingleAsync(cancellationToken);

        return new TaskDetails(
            task.Id,
            task.Title,
            task.Notes,
            task.Status,
            task.Priority,
            task.DueDate,
            task.DueTime,
            task.ReminderOffsetMinutes,
            task.Recurrence,
            task.RecurrenceInterval,
            task.RecurrenceEndDate,
            task.ProjectId,
            task.Project?.Name,
            task.CreatedAt,
            task.CompletedAt,
            task.ChecklistItems
                .OrderBy(item => item.SortOrder)
                .Select(item => new ChecklistItemDto(
                    item.Id,
                    item.Title,
                    item.IsCompleted,
                    item.SortOrder))
                .ToList(),
            task.Tags
                .OrderBy(link => link.Tag.Name)
                .Select(link => new TagRef(link.TagId, link.Tag.Name, link.Tag.Color))
                .ToList(),
            version);
    }

    private static readonly Expression<Func<TodoTask, TaskListItem>> ToListItem = task => new TaskListItem(
        task.Id,
        task.Title,
        task.Status,
        task.Priority,
        task.DueDate,
        task.DueTime,
        task.ProjectId,
        task.Project != null ? task.Project.Name : null,
        task.Tags
            .OrderBy(link => link.Tag.Name)
            .Select(link => new TagRef(link.TagId, link.Tag.Name, link.Tag.Color))
            .ToList(),
        task.ChecklistItems.Count,
        task.ChecklistItems.Count(item => item.IsCompleted),
        task.Recurrence);

    private static IQueryable<TodoTask> ApplyView(
        IQueryable<TodoTask> query,
        TaskView view,
        DateOnly today) => view switch
        {
            TaskView.Inbox => query.Where(task =>
                task.ProjectId == null && task.Status != TodoTaskStatus.Done),
            TaskView.Today => query.Where(task =>
                task.Status != TodoTaskStatus.Done && task.DueDate == today),
            TaskView.Upcoming => query.Where(task =>
                task.Status != TodoTaskStatus.Done
                && task.DueDate != null
                && task.DueDate > today),
            TaskView.Completed => query.Where(task => task.Status == TodoTaskStatus.Done),
            _ => query
        };

    private static IOrderedQueryable<TodoTask> ApplySort(
        IQueryable<TodoTask> query,
        TaskSort sort) => sort switch
        {
            TaskSort.DueDate => query
                .OrderBy(task => task.Status == TodoTaskStatus.Done)
                .ThenBy(task => task.DueDate.HasValue ? 0 : 1)
                .ThenBy(task => task.DueDate)
                .ThenByDescending(task => task.CreatedAt),
            TaskSort.Priority => query
                .OrderBy(task => task.Status == TodoTaskStatus.Done)
                .ThenByDescending(task =>
                    task.Priority == TaskPriority.Urgent ? 4
                    : task.Priority == TaskPriority.High ? 3
                    : task.Priority == TaskPriority.Medium ? 2
                    : task.Priority == TaskPriority.Low ? 1
                    : 0)
                .ThenBy(task => task.DueDate.HasValue ? 0 : 1)
                .ThenBy(task => task.DueDate)
                .ThenByDescending(task => task.CreatedAt),
            TaskSort.Created => query
                .OrderByDescending(task => task.CreatedAt),
            TaskSort.Title => query
                .OrderBy(task => task.Status == TodoTaskStatus.Done)
                .ThenBy(task => task.Title),
            _ => query
                .OrderBy(task => task.Status == TodoTaskStatus.Done)
                .ThenBy(task => task.DueDate.HasValue ? 0 : 1)
                .ThenBy(task => task.DueDate)
                .ThenByDescending(task => task.CreatedAt)
        };

    private DateOnly LocalToday(UserContext user)
        => UserTime.Today(timeProvider, user.TimeZoneId);
}
