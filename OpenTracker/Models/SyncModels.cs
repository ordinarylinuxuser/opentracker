namespace OpenTracker.Models;

public enum SyncTarget
{
    None,
    WebDav,
    S3,
    SFTP
}

public enum SyncInterval
{
    Manual,
    Hourly,
    Daily,
    Weekly
}

public class ActiveTrackingState
{
    public bool IsTracking { get; set; }
    public DateTime StartTime { get; set; }
    public string SelectedTrackerFileName { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

public class BackupData
{
    public DateTime ExportDate { get; set; } = DateTime.UtcNow;
    public List<TrackerManifestItem> Manifest { get; set; } = [];
    public List<TrackerConfig> Configs { get; set; } = [];
    public List<TrackingSession> Sessions { get; set; } = [];
    public ActiveTrackingState? ActiveState { get; set; }
}

public class SyncSettings
{
    public SyncTarget Target { get; set; }
    public SyncInterval Interval { get; set; }
    public string? HostUrl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; } // Consider encryption for production
    public string? BucketName { get; set; } // For S3
    public DateTime LastSyncTime { get; set; }
}