using LiteDB;

namespace OpenTracker.Models;

public class TrackingSession
{
    [BsonId] public Guid Id { get; set; }

    public string TrackerName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double DurationSeconds { get; set; }

    // Sync Properties
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    // ... (Keep existing DurationDisplay and DateDisplay properties)
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

    [BsonIgnore]
    public string DateDisplay
    {
        get
        {
            if (StartTime.Date == EndTime.Date)
            {
                return StartTime.ToString("MMM dd");
            }
            return $"{StartTime:MMM dd} - {EndTime:MMM dd}";
        }
    }
}