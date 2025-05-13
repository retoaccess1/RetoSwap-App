namespace Manta.Helpers;

public static class TimeHelper
{
    public static string FormatDuration(long milliseconds)
    {
        var ts = TimeSpan.FromMilliseconds(milliseconds);

        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m {ts.Seconds}s";
        if (ts.TotalSeconds >= 1)
            return $"{ts.Seconds}s";
        return $"{ts.Milliseconds}ms";
    }
}
