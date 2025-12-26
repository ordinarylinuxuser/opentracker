namespace AnyTracker.Utilities;

public class FormatHelper
{
    public static string FormatTime(TimeSpan span, string format)
    {
        switch (format?.ToLower())
        {
            case "weeks":
                // Useful for Pregnancy (Weeks + Days)
                var totalDays = span.Days;
                var weeks = totalDays / 7;
                var days = totalDays % 7;
                return $"{weeks} Wks, {days} Days";

            case "days":
                // Useful for Habits/Streaks (Days + Hours)
                return $"{span.Days} Days, {span.Hours} Hrs";
            case "time":
            default:
                // Standard HH:MM:SS (handles > 24 hours correctly)
                var totalHours = (int)span.TotalHours;
                return $"{totalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
        }
    }
}