using TodoList.Application.Common;

namespace TodoList.Web.Services;

/// <summary>
/// Resolves the user's local calendar day once per render, falling back to UTC
/// when the stored timezone id is not known to the current host.
/// </summary>
public static class UserClock
{
    public static DateOnly Today(TimeProvider timeProvider, string timeZoneId)
        => UserTime.Today(timeProvider, timeZoneId);
}
