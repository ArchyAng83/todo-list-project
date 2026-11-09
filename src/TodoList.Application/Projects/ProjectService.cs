using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TodoList.Application.Common;
using TodoList.Application.Persistence;
using TodoList.Domain.Projects;

namespace TodoList.Application.Projects;

public sealed class ProjectService(
    IAppDbContext db,
    TimeProvider timeProvider,
    IValidator<CreateProjectRequest> createValidator,
    IValidator<UpdateProjectRequest> updateValidator)
{
    public async Task<Result<Guid>> CreateAsync(
        UserContext user,
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            return Result.Failure<Guid>(ValidationErrors.From(validation));
        }

        var now = timeProvider.GetUtcNow();
        var project = Project.Create(user.UserId, request.Name, now);
        project.SetColor(request.Color, now);

        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(project.Id);
    }

    public async Task<Result> UpdateAsync(
        UserContext user,
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
        {
            return Result.Failure(ValidationErrors.From(validation));
        }

        var project = await FindAsync(user, projectId, cancellationToken);

        if (project is null)
        {
            return Result.Failure(Error.NotFound("Project was not found."));
        }

        var now = timeProvider.GetUtcNow();
        project.Rename(request.Name, now);
        project.SetColor(request.Color, now);

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SetArchivedAsync(
        UserContext user,
        Guid projectId,
        bool isArchived,
        CancellationToken cancellationToken = default)
    {
        var project = await FindAsync(user, projectId, cancellationToken);

        if (project is null)
        {
            return Result.Failure(Error.NotFound("Project was not found."));
        }

        var now = timeProvider.GetUtcNow();

        if (isArchived)
        {
            project.Archive(now);
        }
        else
        {
            project.Unarchive(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SetSortOrderAsync(
        UserContext user,
        Guid projectId,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        var project = await FindAsync(user, projectId, cancellationToken);

        if (project is null)
        {
            return Result.Failure(Error.NotFound("Project was not found."));
        }

        project.SetSortOrder(sortOrder, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(
        UserContext user,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await FindAsync(user, projectId, cancellationToken);

        if (project is null)
        {
            return Result.Failure(Error.NotFound("Project was not found."));
        }

        db.Projects.Remove(project);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private Task<Project?> FindAsync(UserContext user, Guid projectId, CancellationToken cancellationToken)
        => db.Projects.SingleOrDefaultAsync(
            project => project.Id == projectId && project.UserId == user.UserId,
            cancellationToken);
}
