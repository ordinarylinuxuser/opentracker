namespace OpenTracker.Utilities;

public static class FormatHelper
{
    public static string FormatTime(TimeSpan span, string format)
    {
        switch (format?.ToLower())
        {
            case "week":
            case "weeks":
                // Format: "1.5 Weeks"
                var totalDays = span.TotalDays;
                var weeks = (int)(totalDays / 7);
                var days = (int)(totalDays % 7);
                return $"{weeks} Wks, {days} Days";

            case "day":
            case "days":
                // Format: "2.5 Days"
                return $"{span.Days} Days, {span.Hours} Hrs";

            case "time":
            default:
                // Standard HH:MM:SS
                var totalHours = (int)span.TotalHours;
                return $"{totalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
        }
    }
}