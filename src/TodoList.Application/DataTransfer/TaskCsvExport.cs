using System.Globalization;
using System.Text;
using TodoList.Domain.Tasks;

namespace TodoList.Application.DataTransfer;

/// <summary>
/// One flattened task row for the CSV export. Checklist items and tags are
/// rendered as readable inline lists because a single CSV file cannot nest
/// tables.
/// </summary>
public sealed record TaskCsvRow(
    Guid Id,
    string Title,
    TodoTaskStatus Status,
    TaskPriority Priority,
    string? ProjectName,
    DateOnly? DueDate,
    TimeOnly? DueTime,
    int? ReminderOffsetMinutes,
    RecurrenceFrequency Recurrence,
    int RecurrenceInterval,
    DateOnly? RecurrenceEndDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<string> Tags,
    IReadOnlyList<TaskCsvChecklistItem> ChecklistItems);

public sealed record TaskCsvChecklistItem(string Title, bool IsCompleted);

/// <summary>
/// Renders the task CSV export. The output starts with a UTF-8 byte order mark
/// and uses CRLF plus fully quoted fields so spreadsheet applications open it
/// correctly in any locale.
/// </summary>
public static class TaskCsvExport
{
    public const string FileNamePrefix = "todolist-tasks";

    private static readonly string[] Headers =
    [
        "Id",
        "Title",
        "Status",
        "Priority",
        "Project",
        "DueDate",
        "DueTime",
        "ReminderOffsetMinutes",
        "Recurrence",
        "RecurrenceInterval",
        "RecurrenceEndDate",
        "CreatedAt",
        "CompletedAt",
        "Tags",
        "ChecklistItems"
    ];

    public static string Build(IReadOnlyList<TaskCsvRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var builder = new StringBuilder();
        builder.Append('\uFEFF');
        AppendRow(builder, Headers);

        foreach (var row in rows)
        {
            AppendRow(builder,
            [
                row.Id.ToString(),
                row.Title,
                row.Status.ToString(),
                row.Priority.ToString(),
                row.ProjectName ?? string.Empty,
                row.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                row.DueTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty,
                row.ReminderOffsetMinutes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.Recurrence.ToString(),
                row.RecurrenceInterval.ToString(CultureInfo.InvariantCulture),
                row.RecurrenceEndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                row.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
                row.CompletedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                string.Join("; ", row.Tags),
                string.Join("; ", row.ChecklistItems.Select(FormatChecklistItem))
            ]);
        }

        return builder.ToString();
    }

    private static string FormatChecklistItem(TaskCsvChecklistItem item)
        => $"{(item.IsCompleted ? "[x]" : "[ ]")} {item.Title}";

    private static void AppendRow(StringBuilder builder, IEnumerable<string> fields)
    {
        var first = true;

        foreach (var field in fields)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            builder
                .Append('"')
                .Append(field.Replace("\"", "\"\"", StringComparison.Ordinal))
                .Append('"');
        }

        builder.Append("\r\n");
    }
}
