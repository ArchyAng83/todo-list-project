using Microsoft.EntityFrameworkCore;
using TodoList.Application.Common;
using TodoList.Application.Persistence;
using TodoList.Application.Tags;
using TodoList.Domain.Tasks;

namespace TodoList.Application.Tasks;

public sealed class TaskQueries(IAppDbContext db)
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

        return await query
            .OrderBy(task => task.Status == TodoTaskStatus.Done)
            .ThenBy(task => task.DueDate.HasValue ? 0 : 1)
            .ThenBy(task => task.DueDate)
            .ThenByDescending(task => task.CreatedAt)
            .Select(task => new TaskListItem(
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
                task.ChecklistItems.Count(item => item.IsCompleted)))
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

        return new TaskDetails(
            task.Id,
            task.Title,
            task.Notes,
            task.Status,
            task.Priority,
            task.DueDate,
            task.DueTime,
            task.ReminderOffsetMinutes,
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
                .ToList());
    }
}
