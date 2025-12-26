namespace AnyTracker.Models;

public class TrackerManifestItem
{
    public required string Name { get; set; }
    public required string FileName { get; set; }
    public required string Icon { get; set; }
}

public class TrackerConfig
{
    public required string TrackerName { get; set; }
    public string DisplayFormat { get; set; } = "Time";

    public double ElapsedTimeFontSize { get; set; } = 36d;
    public required string ButtonStartText { get; set; }
    public required string ButtonStopText { get; set; }
    public required TrackingStage StoppedState { get; set; }
    public required List<TrackingStage> Stages { get; set; }
}

public class TrackingStage
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public double StartHour { get; set; }
    public double EndHour { get; set; }
    public required string Description { get; set; }
    public required string Icon { get; set; }
    public required string ColorHex { get; set; }

    // Helper for UI to check if this is the active stage
    public bool IsActive { get; set; }
}