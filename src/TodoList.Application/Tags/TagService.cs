using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TodoList.Application.Common;
using TodoList.Application.Persistence;
using TodoList.Domain.Tags;

namespace TodoList.Application.Tags;

public sealed class TagService(
    IAppDbContext db,
    TimeProvider timeProvider,
    IValidator<CreateTagRequest> createValidator,
    IValidator<UpdateTagRequest> updateValidator)
{
    public async Task<Result<Guid>> CreateAsync(
        UserContext user,
        CreateTagRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            return Result.Failure<Guid>(ValidationErrors.From(validation));
        }

        if (await NameExistsAsync(user, request.Name, null, cancellationToken))
        {
            return Result.Failure<Guid>(Error.Conflict("A tag with this name already exists."));
        }

        var now = timeProvider.GetUtcNow();
        var tag = Tag.Create(user.UserId, request.Name, now, request.Color);

        db.Tags.Add(tag);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(tag.Id);
    }

    public async Task<Result> UpdateAsync(
        UserContext user,
        Guid tagId,
        UpdateTagRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            return Result.Failure(ValidationErrors.From(validation));
        }

        var tag = await FindAsync(user, tagId, cancellationToken);

        if (tag is null)
        {
            return Result.Failure(Error.NotFound("Tag was not found."));
        }

        if (await NameExistsAsync(user, request.Name, tagId, cancellationToken))
        {
            return Result.Failure(Error.Conflict("A tag with this name already exists."));
        }

        var now = timeProvider.GetUtcNow();
        tag.Rename(request.Name, now);
        tag.SetColor(request.Color, now);

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(
        UserContext user,
        Guid tagId,
        CancellationToken cancellationToken = default)
    {
        var tag = await FindAsync(user, tagId, cancellationToken);

        if (tag is null)
        {
            return Result.Failure(Error.NotFound("Tag was not found."));
        }

        db.Tags.Remove(tag);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private Task<Tag?> FindAsync(UserContext user, Guid tagId, CancellationToken cancellationToken)
        => db.Tags.SingleOrDefaultAsync(
            tag => tag.Id == tagId && tag.UserId == user.UserId,
            cancellationToken);

    private async Task<bool> NameExistsAsync(
        UserContext user,
        string name,
        Guid? excludedTagId,
        CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLowerInvariant();

        var query = db.Tags.Where(tag =>
            tag.UserId == user.UserId && tag.Name.ToLower() == normalized);

        if (excludedTagId is { } excluded)
        {
            query = query.Where(tag => tag.Id != excluded);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
