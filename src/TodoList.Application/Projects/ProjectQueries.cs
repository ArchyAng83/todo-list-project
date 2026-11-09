using Microsoft.EntityFrameworkCore;
using TodoList.Application.Common;
using TodoList.Application.Persistence;
using TodoList.Domain.Tasks;

namespace TodoList.Application.Projects;

public sealed class ProjectQueries(IAppDbContext db)
{
    public async Task<IReadOnlyList<ProjectListItem>> ListAsync(
        UserContext user,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var query = db.Projects
            .AsNoTracking()
            .Where(project => project.UserId == user.UserId);

        if (!includeArchived)
        {
            query = query.Where(project => !project.IsArchived);
        }

        return await query
            .OrderBy(project => project.IsArchived)
            .ThenBy(project => project.SortOrder)
            .ThenBy(project => project.Name)
            .Select(project => new ProjectListItem(
                project.Id,
                project.Name,
                project.Color,
                project.IsArchived,
                project.SortOrder,
                db.Tasks.Count(task =>
                    task.ProjectId == project.Id && task.Status != TodoTaskStatus.Done),
                db.Tasks.Count(task => task.ProjectId == project.Id)))
            .ToListAsync(cancellationToken);
    }
}
