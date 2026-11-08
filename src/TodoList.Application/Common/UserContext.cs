namespace TodoList.Application.Common;

public sealed record UserContext(Guid UserId, string TimeZoneId)
{
    public static UserContext System => new(Guid.Empty, "UTC");
}
