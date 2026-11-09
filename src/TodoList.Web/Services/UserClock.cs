namespace TodoList.Web.Services;

/// <summary>
/// Resolves the user's local calendar day once per render, falling back to UTC
/// when the stored timezone id is not known to the current host.
/// </summary>
public static class UserClock
{
    public static DateOnly Today(TimeProvider timeProvider, string timeZoneId)
    {
        TimeZoneInfo zone;

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception)
            when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
        }

        return DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), zone).DateTime);
    }
}
