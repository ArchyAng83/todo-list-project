using TodoList.Domain.Tasks;

namespace TodoList.Web.Components.Tasks;

/// <summary>
/// A card move requested by the board. <see cref="TargetIndex"/> is the final
/// zero-based position in the target column after the card has been removed from
/// its current position.
/// </summary>
public sealed record TaskMoveArgs(Guid TaskId, TodoTaskStatus Status, int TargetIndex);
