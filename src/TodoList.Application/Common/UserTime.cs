namespace TodoList.Application.Common;

/// <summary>
/// Converts between the user's local calendar values and UTC. Timezone ids
/// come from user settings, so an unknown id falls back to UTC instead of
/// failing the request.
/// </summary>
public static class UserTime
{
    public static DateOnly Today(TimeProvider timeProvider, string timeZoneId)
        => DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Resolve(timeZoneId)).DateTime);

    /// <summary>
    /// Converts a local date and time to a UTC instant. A date-only value uses
    /// the supplied default time (09:00 for reminders), and an unknown timezone
    /// falls back to UTC.
    /// </summary>
    public static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, string timeZoneId)
    {
        var zone = Resolve(timeZoneId);
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        var offset = zone.GetUtcOffset(local);

        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    public static TimeZoneInfo Resolve(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception)
            when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
