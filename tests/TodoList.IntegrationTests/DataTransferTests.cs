using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TodoList.Application.Common;
using TodoList.Application.DataTransfer;
using TodoList.Application.Projects;
using TodoList.Application.Tags;
using TodoList.Domain.Tasks;
using TodoList.Infrastructure.Identity;
using TodoList.Testing;

namespace TodoList.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class DataTransferTests(TodoListAppFixture fixture)
{
    private static readonly DateTimeOffset ImportedCreatedAt =
        new(2020, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ImportThenExport_RoundTripsProjectsTagsAndTasks()
    {
        var user = await CreateUserContextAsync();
        var document = CreateDocument();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<DataTransferService>();

        var imported = await service.ImportAsync(user, document);

        imported.IsSuccess.ShouldBeTrue(imported.IsFailure ? imported.Error.Message : string.Empty);
        imported.Value.ProjectsCreated.ShouldBe(1);
        imported.Value.TagsCreated.ShouldBe(1);
        imported.Value.TasksCreated.ShouldBe(1);
        imported.Value.ChecklistItemsCreated.ShouldBe(2);

        var exported = await service.ExportAsync(user);

        exported.Version.ShouldBe(ExportDocument.CurrentVersion);
        exported.Projects.ShouldHaveSingleItem();
        exported.Tags.ShouldHaveSingleItem();
        exported.Tasks.ShouldHaveSingleItem();

        var project = exported.Projects[0];
        var tag = exported.Tags[0];
        var task = exported.Tasks[0];

        project.Name.ShouldBe("Work");
        project.Color.ShouldBe("#4F46E5");
        tag.Name.ShouldBe("urgent");
        tag.Color.ShouldBe("#EF4444");

        task.Title.ShouldBe("Prepare release");
        task.Notes.ShouldBe("Cut the release and publish notes.");
        task.Status.ShouldBe(TodoTaskStatus.InProgress);
        task.Priority.ShouldBe(TaskPriority.High);
        task.DueDate.ShouldBe(new DateOnly(2026, 12, 10));
        task.DueTime.ShouldBe(new TimeOnly(9, 30));
        task.ReminderOffsetMinutes.ShouldBe(15);
        task.Recurrence.ShouldBe(RecurrenceFrequency.Weekly);
        task.RecurrenceInterval.ShouldBe(2);
        task.RecurrenceEndDate.ShouldBe(new DateOnly(2027, 1, 31));
        task.ProjectId.ShouldBe(project.Id);
        task.TagIds.ShouldBe([tag.Id]);
        task.CreatedAt.ShouldBeGreaterThan(ImportedCreatedAt);
        task.ChecklistItems.Count.ShouldBe(2);
        task.ChecklistItems[0].Title.ShouldBe("Draft the notes");
        task.ChecklistItems[0].IsCompleted.ShouldBeTrue();
        task.ChecklistItems[1].Title.ShouldBe("Publish the notes");
        task.ChecklistItems[1].IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Import_SameDocumentTwice_ReusesProjectsAndTagsButAddsTasks()
    {
        var user = await CreateUserContextAsync();
        var document = CreateDocument();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<DataTransferService>();

        (await service.ImportAsync(user, document)).IsSuccess.ShouldBeTrue();
        var second = await service.ImportAsync(user, document);

        second.IsSuccess.ShouldBeTrue(second.IsFailure ? second.Error.Message : string.Empty);
        second.Value.ProjectsCreated.ShouldBe(0);
        second.Value.ProjectsReused.ShouldBe(1);
        second.Value.TagsCreated.ShouldBe(0);
        second.Value.TagsReused.ShouldBe(1);
        second.Value.TasksCreated.ShouldBe(1);

        var exported = await service.ExportAsync(user);

        exported.Projects.ShouldHaveSingleItem();
        exported.Tags.ShouldHaveSingleItem();
        exported.Tasks.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Import_ReusesExistingProjectAndTagNamesCaseInsensitively()
    {
        var user = await CreateUserContextAsync();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var projectService = scope.ServiceProvider.GetRequiredService<ProjectService>();
        var tagService = scope.ServiceProvider.GetRequiredService<TagService>();
        var service = scope.ServiceProvider.GetRequiredService<DataTransferService>();

        var projectId = (await projectService.CreateAsync(
            user,
            new CreateProjectRequest("Work"))).Value;
        var tagId = (await tagService.CreateAsync(
            user,
            new CreateTagRequest("urgent"))).Value;

        var document = CreateDocument(projectName: " work ", tagName: "URGENT");
        var result = await service.ImportAsync(user, document);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Message : string.Empty);
        result.Value.ProjectsCreated.ShouldBe(0);
        result.Value.ProjectsReused.ShouldBe(1);
        result.Value.TagsCreated.ShouldBe(0);
        result.Value.TagsReused.ShouldBe(1);

        var exported = await service.ExportAsync(user);

        exported.Projects.ShouldHaveSingleItem().Id.ShouldBe(projectId);
        exported.Tags.ShouldHaveSingleItem().Id.ShouldBe(tagId);
        exported.Tasks.ShouldHaveSingleItem().ProjectId.ShouldBe(projectId);
        exported.Tasks[0].TagIds.ShouldBe([tagId]);
    }

    [Fact]
    public async Task Export_ScopesTasksProjectsAndTagsToTheOwner()
    {
        var owner = await CreateUserContextAsync();
        var other = await CreateUserContextAsync();

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<DataTransferService>();

        (await service.ImportAsync(owner, CreateDocument(taskTitle: "Owner task"))).IsSuccess.ShouldBeTrue();
        (await service.ImportAsync(other, CreateDocument(taskTitle: "Other task"))).IsSuccess.ShouldBeTrue();

        var exported = await service.ExportAsync(owner);

        exported.Tasks.ShouldHaveSingleItem().Title.ShouldBe("Owner task");
        exported.Projects.ShouldHaveSingleItem().Name.ShouldBe("Work");
        exported.Tags.ShouldHaveSingleItem().Name.ShouldBe("urgent");
    }

    [Fact]
    public async Task Import_UnsupportedVersion_ReturnsValidationFailure()
    {
        var user = await CreateUserContextAsync();
        var document = CreateDocument() with { Version = ExportDocument.CurrentVersion + 1 };

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<DataTransferService>();

        var result = await service.ImportAsync(user, document);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("validation");
        result.Error.Message.ShouldContain("Unsupported export version");
        (await service.ExportAsync(user)).Tasks.ShouldBeEmpty();
    }

    [Fact]
    public async Task Import_TaskWithUnknownProject_ReturnsValidationFailureAndSavesNothing()
    {
        var user = await CreateUserContextAsync();
        var document = CreateDocument() with
        {
            Tasks =
            [
                CreateDocument().Tasks[0] with { ProjectId = Guid.CreateVersion7() }
            ]
        };

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<DataTransferService>();

        var result = await service.ImportAsync(user, document);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("validation");
        (await service.ExportAsync(user)).Tasks.ShouldBeEmpty();
        (await service.GetSummaryAsync(user)).Projects.ShouldBe(0);
    }

    [Fact]
    public async Task Import_CompletedRecurringTask_DoesNotGenerateNextOccurrence()
    {
        var user = await CreateUserContextAsync();
        var document = CreateDocument() with
        {
            Tasks =
            [
                new ExportTask(
                    Guid.CreateVersion7(),
                    null,
                    "Done recurring",
                    null,
                    TodoTaskStatus.Done,
                    TaskPriority.Medium,
                    null,
                    null,
                    null,
                    RecurrenceFrequency.None,
                    1,
                    null,
                    0,
                    ImportedCreatedAt,
                    null,
                    [],
                    [])
            ]
        };

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<DataTransferService>();

        var result = await service.ImportAsync(user, document);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Message : string.Empty);

        var exported = await service.ExportAsync(user);

        exported.Tasks.ShouldHaveSingleItem().Status.ShouldBe(TodoTaskStatus.Done);
    }

    private static ExportDocument CreateDocument(
        string projectName = "Work",
        string tagName = "urgent",
        string taskTitle = "Prepare release")
    {
        var projectId = Guid.CreateVersion7();
        var tagId = Guid.CreateVersion7();
        var taskId = Guid.CreateVersion7();

        return new ExportDocument(
            ExportDocument.CurrentVersion,
            DateTimeOffset.UtcNow,
            [new ExportProject(projectId, projectName, "#4F46E5", false, 0)],
            [new ExportTag(tagId, tagName, "#EF4444")],
            [
                new ExportTask(
                    taskId,
                    projectId,
                    taskTitle,
                    "Cut the release and publish notes.",
                    TodoTaskStatus.InProgress,
                    TaskPriority.High,
                    new DateOnly(2026, 12, 10),
                    new TimeOnly(9, 30),
                    15,
                    RecurrenceFrequency.Weekly,
                    2,
                    new DateOnly(2027, 1, 31),
                    0,
                    ImportedCreatedAt,
                    null,
                    [tagId],
                    [
                        new ExportChecklistItem("Draft the notes", true, 0),
                        new ExportChecklistItem("Publish the notes", false, 1)
                    ])
            ]);
    }

    private async Task<UserContext> CreateUserContextAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var email = $"data-{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            DisplayName = "Data User",
            Culture = "en",
            TimeZoneId = "UTC",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, "Password1");
        result.Succeeded.ShouldBeTrue(
            string.Join(", ", result.Errors.Select(error => error.Description)));

        return new UserContext(user.Id, user.TimeZoneId);
    }
}
