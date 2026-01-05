#region

using LiteDB;

#endregion

namespace OpenTracker.Models;

public class TrackingSession
{
    [BsonId] public int Id { get; set; }

    public string TrackerName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double DurationSeconds { get; set; }

    // Not mapped to DB, just for UI
    [BsonIgnore]
    public string DurationDisplay
    {
        get
        {
            var span = TimeSpan.FromSeconds(DurationSeconds);
            if (span.TotalHours >= 1) return $"{span.TotalHours:F1} hrs";
            return $"{span.TotalMinutes:F0} mins";
        }
    }

    // New property for smart date display
    [BsonIgnore]
    public string DateDisplay
    {
        get
        {
            // If it ends on the same day, just show one date (e.g. "Jan 01")
            if (StartTime.Date == EndTime.Date)
            {
                return StartTime.ToString("MMM dd");
            }
            // If it crosses midnight, show range (e.g. "Jan 01 - Jan 02")
            return $"{StartTime:MMM dd} - {EndTime:MMM dd}";
        }
    }
}