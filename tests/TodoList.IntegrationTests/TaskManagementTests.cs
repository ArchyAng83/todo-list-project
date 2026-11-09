using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TodoList.Application.Common;
using TodoList.Application.Projects;
using TodoList.Application.Tags;
using TodoList.Application.Tasks;
using TodoList.Domain.Tasks;
using TodoList.Infrastructure.Identity;
using TodoList.Testing;

namespace TodoList.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class TaskManagementTests(TodoListAppFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProjectService_CreateAndArchive_ScopesToOwner()
    {
        var userId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var user = new UserContext(userId, "UTC");
        var otherUser = new UserContext(otherUserId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectService>();
        var queries = scope.ServiceProvider.GetRequiredService<ProjectQueries>();

        var created = await service.CreateAsync(user, new CreateProjectRequest("Work", "#4F46E5"));

        created.IsSuccess.ShouldBeTrue();
        created.Value.ShouldNotBe(Guid.Empty);

        var projects = await queries.ListAsync(user);
        projects.ShouldHaveSingleItem().Name.ShouldBe("Work");

        (await queries.ListAsync(otherUser)).ShouldBeEmpty();
        (await service.SetArchivedAsync(otherUser, created.Value, true)).Error.Code.ShouldBe("not_found");
        (await service.DeleteAsync(otherUser, created.Value)).Error.Code.ShouldBe("not_found");

        (await service.SetArchivedAsync(user, created.Value, true)).IsSuccess.ShouldBeTrue();

        (await queries.ListAsync(user)).ShouldBeEmpty();
        (await queries.ListAsync(user, includeArchived: true))
            .ShouldHaveSingleItem()
            .IsArchived.ShouldBeTrue();
    }

    [Fact]
    public async Task ProjectQueries_CountOpenAndTotalTasks()
    {
        var userId = await CreateUserAsync();
        var user = new UserContext(userId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var projectService = scope.ServiceProvider.GetRequiredService<ProjectService>();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        var projectQueries = scope.ServiceProvider.GetRequiredService<ProjectQueries>();

        var projectId = (await projectService.CreateAsync(user, new CreateProjectRequest("Inbox"))).Value;
        var first = (await taskService.CreateAsync(user, new CreateTaskRequest("First", projectId))).Value;
        await taskService.CreateAsync(user, new CreateTaskRequest("Second", projectId));
        await taskService.SetStatusAsync(user, first, TodoTaskStatus.Done);

        var project = (await projectQueries.ListAsync(user)).ShouldHaveSingleItem();

        project.OpenTaskCount.ShouldBe(1);
        project.TotalTaskCount.ShouldBe(2);
    }

    [Fact]
    public async Task TagService_DuplicateName_ReturnsConflictIgnoringCase()
    {
        var userId = await CreateUserAsync();
        var user = new UserContext(userId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<TagService>();
        var queries = scope.ServiceProvider.GetRequiredService<TagQueries>();

        (await service.CreateAsync(user, new CreateTagRequest("Deep Work", "#6B7280"))).IsSuccess.ShouldBeTrue();

        var duplicate = await service.CreateAsync(user, new CreateTagRequest("deep work"));

        duplicate.IsFailure.ShouldBeTrue();
        duplicate.Error.Code.ShouldBe("conflict");
        (await queries.ListAsync(user)).ShouldHaveSingleItem().Name.ShouldBe("Deep Work");
    }

    [Fact]
    public async Task TaskService_UpdateChecklistAndTags_PersistsDetails()
    {
        var userId = await CreateUserAsync();
        var user = new UserContext(userId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var projectService = scope.ServiceProvider.GetRequiredService<ProjectService>();
        var tagService = scope.ServiceProvider.GetRequiredService<TagService>();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        var taskQueries = scope.ServiceProvider.GetRequiredService<TaskQueries>();

        var projectId = (await projectService.CreateAsync(user, new CreateProjectRequest("Home"))).Value;
        var tagId = (await tagService.CreateAsync(user, new CreateTagRequest("urgent", "#EF4444"))).Value;
        var taskId = (await taskService.CreateAsync(user, new CreateTaskRequest("Buy milk"))).Value;

        var update = await taskService.UpdateAsync(
            user,
            taskId,
            new UpdateTaskRequest(
                "Buy milk",
                "Two litres",
                projectId,
                TaskPriority.Medium,
                new DateOnly(2026, 11, 10),
                new TimeOnly(18, 30),
                30));

        update.IsSuccess.ShouldBeTrue();

        var checklistItemId = (await taskService.AddChecklistItemAsync(
            user,
            taskId,
            new AddChecklistItemRequest("Check the fridge"))).Value;

        (await taskService.SetChecklistItemCompletedAsync(
            user,
            taskId,
            checklistItemId,
            new SetChecklistItemCompletedRequest(true))).IsSuccess.ShouldBeTrue();

        (await taskService.AddTagAsync(user, taskId, new AddTaskTagRequest(tagId))).IsSuccess.ShouldBeTrue();

        var details = await taskQueries.GetAsync(user, taskId);

        details.ShouldNotBeNull();
        details.Notes.ShouldBe("Two litres");
        details.ProjectName.ShouldBe("Home");
        details.Priority.ShouldBe(TaskPriority.Medium);
        details.DueDate.ShouldBe(new DateOnly(2026, 11, 10));
        details.DueTime.ShouldBe(new TimeOnly(18, 30));
        details.ReminderOffsetMinutes.ShouldBe(30);
        details.ChecklistItems.ShouldHaveSingleItem().IsCompleted.ShouldBeTrue();
        details.Tags.ShouldHaveSingleItem().Name.ShouldBe("urgent");
    }

    [Fact]
    public async Task TaskQueries_FilterByProjectTagAndSearch()
    {
        var userId = await CreateUserAsync();
        var user = new UserContext(userId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var projectService = scope.ServiceProvider.GetRequiredService<ProjectService>();
        var tagService = scope.ServiceProvider.GetRequiredService<TagService>();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        var taskQueries = scope.ServiceProvider.GetRequiredService<TaskQueries>();

        var alphaProject = (await projectService.CreateAsync(user, new CreateProjectRequest("Alpha"))).Value;
        var betaProject = (await projectService.CreateAsync(user, new CreateProjectRequest("Beta"))).Value;
        var urgentTag = (await tagService.CreateAsync(user, new CreateTagRequest("urgent"))).Value;

        var alphaTask = (await taskService.CreateAsync(user, new CreateTaskRequest("Alpha report", alphaProject))).Value;
        await taskService.CreateAsync(user, new CreateTaskRequest("Beta review", betaProject));
        await taskService.AddTagAsync(user, alphaTask, new AddTaskTagRequest(urgentTag));
        await taskService.UpdateAsync(
            user,
            alphaTask,
            new UpdateTaskRequest("Alpha report", "Quarterly numbers", alphaProject, TaskPriority.None, null, null, null));

        (await taskQueries.ListAsync(user, new TaskListFilter(ProjectId: alphaProject)))
            .ShouldHaveSingleItem()
            .Id.ShouldBe(alphaTask);

        (await taskQueries.ListAsync(user, new TaskListFilter(TagId: urgentTag)))
            .ShouldHaveSingleItem()
            .Id.ShouldBe(alphaTask);

        var search = await taskQueries.ListAsync(user, new TaskListFilter(Search: "quarterly"));

        search.ShouldHaveSingleItem().Id.ShouldBe(alphaTask);
        (await taskQueries.ListAsync(user, new TaskListFilter(Search: "missing"))).ShouldBeEmpty();
    }

    [Fact]
    public async Task TaskService_CrossUserAccess_ReturnsNotFound()
    {
        var ownerId = await CreateUserAsync();
        var intruderId = await CreateUserAsync();
        var owner = new UserContext(ownerId, "UTC");
        var intruder = new UserContext(intruderId, "UTC");

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var projectService = scope.ServiceProvider.GetRequiredService<ProjectService>();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
        var taskQueries = scope.ServiceProvider.GetRequiredService<TaskQueries>();

        var projectId = (await projectService.CreateAsync(owner, new CreateProjectRequest("Private"))).Value;
        var taskId = (await taskService.CreateAsync(owner, new CreateTaskRequest("Secret", projectId))).Value;

        (await taskQueries.GetAsync(intruder, taskId)).ShouldBeNull();
        (await taskService.SetStatusAsync(intruder, taskId, TodoTaskStatus.Done)).Error.Code.ShouldBe("not_found");
        (await taskService.DeleteAsync(intruder, taskId)).Error.Code.ShouldBe("not_found");
        (await taskService.CreateAsync(
            intruder,
            new CreateTaskRequest("Intrusion", projectId))).Error.Code.ShouldBe("not_found");
        (await taskService.UpdateAsync(
            intruder,
            taskId,
            new UpdateTaskRequest("Hijacked", null, null, TaskPriority.None, null, null, null)))
            .Error.Code.ShouldBe("not_found");
    }

    private async Task<Guid> CreateUserAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var email = $"svc-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            DisplayName = "Service User",
            Culture = "en",
            CreatedAt = Now
        };

        var result = await userManager.CreateAsync(user, "Password1");
        result.Succeeded.ShouldBeTrue(
            string.Join(", ", result.Errors.Select(error => error.Description)));

        return user.Id;
    }
}
