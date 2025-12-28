#region

using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiteDB;

#endregion

namespace OpenTracker.Models;

public class TrackerManifestItem
{
    public required string Name { get; set; }
    [BsonId] public required string FileName { get; set; }
    public required string Icon { get; set; }
}

public class TrackerConfig : INotifyPropertyChanged
{
    private string _durationType = "Time";

    // We add FileName as the ID to retrieve config by the filename in the manifest
    [BsonId] public string FileName { get; set; } = string.Empty;

    public string TrackerName { get; set; } = string.Empty;
    public string DurationType
    {
        get => _durationType;
        set
        {
            if (_durationType != value)
            {
                _durationType = value;
                OnPropertyChanged();
                // Notify that Font Size has changed because it depends on DurationType
                OnPropertyChanged(nameof(ElapsedTimeFontSize));
            }
        }
    }

    // Computed Property: Logic for Font Size
    [BsonIgnore]
    public double ElapsedTimeFontSize
    {
        get
        {
            // If "Time" -> 36, Else (Day/Week) -> 16
            if (string.Equals(DurationType, "Time", StringComparison.OrdinalIgnoreCase))
            {
                return 36d;
            }
            return 16d;
        }
    }

    public required string ButtonStartText { get; set; }
    public required string ButtonStopText { get; set; }
    public required TrackingStage StoppedState { get; set; }
    public required List<TrackingStage> Stages { get; set; } = [];

    // -- INotifyPropertyChanged Implementation --
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}

public class TrackingStage : INotifyPropertyChanged
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public double Start { get; set; }
    public double End { get; set; }
    public required string Description { get; set; }
    public required string Icon { get; set; }
    public required string ColorHex { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // -- DYNAMIC PROPERTIES --
    private bool _isActive;

    // 1. The trigger flag
    [BsonIgnore]
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();

                // 2. IMPORTANT: Notify that the Color has also changed!
                OnPropertyChanged(nameof(TitleColor));
            }
        }
    }

    // 3. The computed color property for the UI to bind to
    [BsonIgnore]
    public Color TitleColor
    {
        get
        {
            if (IsActive)
            {
                // Parse the hex string to a MAUI Color
                return Color.FromArgb(ColorHex);
            }

            // Return the inactive gray color (matching your previous #CCC)
            return Color.FromArgb("#CCCCCC");
        }
    }

}