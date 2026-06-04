namespace UkManagement.Web.Domain;

public static class DisplayTime
{
    private static readonly TimeSpan MoscowOffset = TimeSpan.FromHours(3);

    public static DateTimeOffset Now => DateTimeOffset.UtcNow.ToOffset(MoscowOffset);

    public static DateTime Today => Now.Date;

    public static DateTimeOffset StartOfDay(DateTime date)
    {
        return new DateTimeOffset(date.Date, MoscowOffset);
    }

    public static DateTimeOffset StartOfDayUtc(DateTime date)
    {
        return StartOfDay(date).ToUniversalTime();
    }

    public static DateTimeOffset ToDisplayTime(this DateTimeOffset value)
    {
        return value.ToOffset(MoscowOffset);
    }
}
