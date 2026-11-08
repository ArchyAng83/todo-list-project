using TodoList.Domain.Tasks;

namespace TodoList.Domain.Tags;

public sealed class TaskTag
{
    private TaskTag()
    {
        // Required by EF Core.
    }

    internal TaskTag(Guid taskId, Guid tagId)
    {
        TaskId = taskId;
        TagId = tagId;
    }

    public Guid TaskId { get; private set; }

    public Guid TagId { get; private set; }

    public TodoTask Task { get; private set; } = null!;

    public Tag Tag { get; private set; } = null!;
}
