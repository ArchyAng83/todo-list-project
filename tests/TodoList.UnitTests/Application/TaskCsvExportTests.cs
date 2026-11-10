using Shouldly;
using TodoList.Application.DataTransfer;
using TodoList.Domain.Tasks;

namespace TodoList.UnitTests.Application;

public sealed class TaskCsvExportTests
{
    [Fact]
    public void Build_EmptyRows_WritesBomAndHeaderWithCrlf()
    {
        var csv = TaskCsvExport.Build([]);

        csv[0].ShouldBe('\uFEFF');
        csv.ShouldStartWith("\uFEFF\"Id\",\"Title\",\"Status\",\"Priority\",\"Project\"");
        csv.ShouldEndWith("\"ChecklistItems\"\r\n");
    }

    [Fact]
    public void Build_FieldWithCommasQuotesAndNewlines_EscapesQuotes()
    {
        var csv = TaskCsvExport.Build([CreateRow(title: "Pay, \"rent\"\nnow")]);

        csv.ShouldContain("\"Pay, \"\"rent\"\"\nnow\"");
    }

    [Fact]
    public void Build_RowWithTagsAndChecklist_FormatsInlineLists()
    {
        var row = CreateRow(
            title: "Release",
            tags: ["work", "release"],
            checklist:
            [
                new TaskCsvChecklistItem("Run tests", true),
                new TaskCsvChecklistItem("Tag the build", false)
            ]);

        var csv = TaskCsvExport.Build([row]);

        csv.ShouldContain("\"work; release\"");
        csv.ShouldContain("\"[x] Run tests; [ ] Tag the build\"");
    }

    [Fact]
    public void Build_RowWithDatesAndEnums_UsesInvariantFormats()
    {
        var row = CreateRow(
            title: "Plan",
            dueDate: new DateOnly(2026, 12, 9),
            dueTime: new TimeOnly(8, 5),
            recurrence: RecurrenceFrequency.Weekly,
            recurrenceInterval: 2);

        var csv = TaskCsvExport.Build([row]);

        csv.ShouldContain("\"2026-12-09\"");
        csv.ShouldContain("\"08:05\"");
        csv.ShouldContain("\"Weekly\"");
        csv.ShouldContain("\"2\"");
    }

    [Fact]
    public void ExportDocument_CurrentVersion_IsOne()
    {
        ExportDocument.CurrentVersion.ShouldBe(1);
    }

    private static TaskCsvRow CreateRow(
        string title,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<TaskCsvChecklistItem>? checklist = null,
        DateOnly? dueDate = null,
        TimeOnly? dueTime = null,
        RecurrenceFrequency recurrence = RecurrenceFrequency.None,
        int recurrenceInterval = 1)
        => new(
            Guid.CreateVersion7(),
            title,
            TodoTaskStatus.Todo,
            TaskPriority.Medium,
            "Portfolio",
            dueDate,
            dueTime,
            null,
            recurrence,
            recurrenceInterval,
            null,
            new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero),
            null,
            tags ?? [],
            checklist ?? []);
}
