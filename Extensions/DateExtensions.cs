namespace Manta.Extensions;

public static class DateExtensions
{
    public static DateTime ToDateTime(this long date)
    {
        return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(date);
    }

    public static DateTime ToDateTime(this ulong date)
    {
        return ((long)date).ToDateTime();
    }
}
