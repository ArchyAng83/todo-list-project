using Microsoft.EntityFrameworkCore;
using TodoList.Application.Common;
using TodoList.Application.Persistence;

namespace TodoList.Application.Tags;

public sealed class TagQueries(IAppDbContext db)
{
    public async Task<IReadOnlyList<TagListItem>> ListAsync(
        UserContext user,
        CancellationToken cancellationToken = default)
    {
        return await db.Tags
            .AsNoTracking()
            .Where(tag => tag.UserId == user.UserId)
            .OrderBy(tag => tag.Name)
            .Select(tag => new TagListItem(
                tag.Id,
                tag.Name,
                tag.Color,
                db.TaskTags.Count(link => link.TagId == tag.Id)))
            .ToListAsync(cancellationToken);
    }
}
