#region

using OpenTracker.Constants;
using OpenTracker.Models;
using OpenTracker.Pages;
using OpenTracker.Services;
using OpenTracker.Utilities;
using System.Collections.ObjectModel;
using System.Windows.Input;

#endregion

namespace OpenTracker.ViewModels;

public class MainViewModel : BindableObject
{
    #region Private Fields

    private readonly IDbService _dbService;
    private readonly TrackerService _trackerService;
    private readonly INotificationService _notificationService;
    private TrackerConfig _currentConfig;
    private TrackingStage _currentStage;
    private string _elapsedTime = "00:00:00";
    private bool _isTracking;
    private string _startStopButtonText;
    private DateTime _startTime;
    private IDispatcherTimer _timer;
    private string _trackerTitle;

    private double _totalDurationValue;

    #endregion

    #region Constructor

    public MainViewModel(INotificationService notificationService,
        IDbService dbService,
        TrackerService trackerService)
    {
        _trackerService = trackerService;
        _dbService = dbService;
        _notificationService = notificationService;
        ToggleTrackingCommand = new Command(ToggleTracking);
        EditElapsedTimeCommand = new Command(async () => await EditElapsedTime());
        _trackerService.OnTrackerChanged += OnConfigChanged;
        if (_trackerService.CurrentConfig != null) OnConfigChanged();
    }

    #endregion

    #region Binding Properties

    public ObservableCollection<TrackingStage> Stages { get; set; } = [];

    public DateTime StartTime => _startTime;

    public TrackerConfig CurrentConfig => _currentConfig; // Exposed for Drawable

    public TrackingStage CurrentStage
    {
        get => _currentStage;
        set
        {
            _currentStage = value;
            OnPropertyChanged();
        }
    }

    public string TrackerTitle
    {
        get => _trackerTitle;
        set
        {
            _trackerTitle = value;
            OnPropertyChanged();
        }
    }

    public string ElapsedTime
    {
        get => _elapsedTime;
        set
        {
            _elapsedTime = value;
            OnPropertyChanged();
        }
    }

    public string StartStopButtonText
    {
        get => _startStopButtonText;
        set
        {
            _startStopButtonText = value;
            OnPropertyChanged();
        }
    }

    public bool IsTracking
    {
        get => _isTracking;
        set
        {
            _isTracking = value;
            OnPropertyChanged();
        }
    }

    public double TotalDurationValue
    {
        get => _totalDurationValue;
        set
        {
            if (Math.Abs(_totalDurationValue - value) > 0.0001)
            {
                _totalDurationValue = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region Commands

    public ICommand ToggleTrackingCommand { get; }
    public ICommand EditElapsedTimeCommand { get; }

    #endregion

    #region Methods

    private void OnConfigChanged()
    {
        if (IsTracking) StopTracking();

        _currentConfig = _trackerService.CurrentConfig;
        if (_currentConfig == null) return;
        // CRITICAL FIX: Notify the UI that the config object is new
        // This makes the UI re-bind "CurrentConfig.ElapsedTimeFontSize"
        OnPropertyChanged(nameof(CurrentConfig));

        var wasTracking = Preferences.Get(AppConstants.PrefIsTracking, false);

        ApplyConfig();

        if (wasTracking) RestoreTrackingState();
    }

    private void ApplyConfig()
    {
        Stages.Clear();
        foreach (var s in _currentConfig.Stages)
        {
            Stages.Add(s);
        }

        TrackerTitle = _currentConfig.TrackerName;

        if (!IsTracking)
        {
            CurrentStage = _currentConfig.StoppedState;
            TotalDurationValue = 0;
            StartStopButtonText = _currentConfig.ButtonStartText ?? "Start Tracking";
        }
    }

    private void RestoreTrackingState()
    {
        var savedTime = Preferences.Get(AppConstants.PrefStartTime, DateTime.MinValue);
        if (savedTime != DateTime.MinValue)
        {
            _startTime = savedTime;
            StartTrackingInternal(true);
        }
    }

    private void ToggleTracking()
    {
        if (IsTracking) StopTracking();
        else StartTracking();
    }

    private void UpdateActiveStateTimestamp()
    {
        // Store as ISO 8601 string for reliable persistence in Preferences
        Preferences.Set(AppConstants.PrefActiveStateModified, DateTime.UtcNow.ToString("O"));
    }

    private void StartTracking()
    {
        _startTime = DateTime.Now;
        Preferences.Set(AppConstants.PrefIsTracking, true);
        Preferences.Set(AppConstants.PrefStartTime, _startTime);
        UpdateActiveStateTimestamp(); // Record that we changed the state
        StartTrackingInternal(false);
    }

    private void StartTrackingInternal(bool resume)
    {
        IsTracking = true;
        StartStopButtonText = _currentConfig.ButtonStopText ?? "Stop Tracking";

        if (_timer == null)
        {
            _timer = Application.Current.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => UpdateProgress();
        }

        _timer.Start();

        _notificationService.StartNotification(
            TrackerTitle,
            CurrentStage?.Title ?? "Tracking...",
            _startTime,
            _currentConfig?.DurationType ?? "Time");

        UpdateProgress();
    }

    private async void StopTracking()
    {
        if (IsTracking)
        {
            var endTime = DateTime.Now;
            var session = new TrackingSession
            {
                TrackerName = _currentConfig?.TrackerName ?? "Unknown",
                StartTime = _startTime,
                EndTime = endTime,
                DurationSeconds = (endTime - _startTime).TotalSeconds
            };

            await _dbService.AddSessionAsync(session);
        }

        IsTracking = false;
        Preferences.Set(AppConstants.PrefIsTracking, false);
        Preferences.Remove(AppConstants.PrefStartTime);
        UpdateActiveStateTimestamp(); // Record that we changed the state
        _timer?.Stop();
        _notificationService.StopNotification();

        if (_currentConfig != null)
        {
            CurrentStage = _currentConfig.StoppedState;
            StartStopButtonText = _currentConfig.ButtonStartText ?? "Start Tracking";
        }

        ElapsedTime = "00:00:00";
        // FIX: Reset progress value so the Circle and Badge reset immediately
        TotalDurationValue = 0;
        foreach (var s in Stages) s.IsActive = false;
    }

    private void UpdateProgress()
    {
        var elapsed = DateTime.Now - _startTime;
        if (_currentConfig != null)
        {
            ElapsedTime = FormatHelper.FormatTime(elapsed, _currentConfig.DurationType);

            // Calculate Value based on DurationType
            double currentValue = 0;
            switch (_currentConfig.DurationType?.ToLower())
            {
                case "week":
                case "weeks":
                    currentValue = elapsed.TotalDays / 7.0; // Value in Weeks
                    break;
                case "day":
                case "days":
                    currentValue = elapsed.TotalDays; // Value in Days
                    break;
                case "time":
                default:
                    currentValue = elapsed.TotalHours; // Value in Hours
                    break;
            }

            TotalDurationValue = currentValue;

            // Check Stages against generic Start/End
            var activeStage = Stages.FirstOrDefault(s => TotalDurationValue >= s.Start && TotalDurationValue < s.End);

            if (activeStage == null && Stages.Any() && TotalDurationValue > Stages.Last().End)
                activeStage = Stages.Last();

            if (activeStage != null && CurrentStage != activeStage)
            {
                CurrentStage = activeStage;
                _notificationService.UpdateStage(CurrentStage.Title);

            }

            foreach (var s in Stages) s.IsActive = s == activeStage;
        }
    }

    private async Task EditElapsedTime()
    {
        await Shell.Current.Navigation.PushModalAsync(new ManualEntryPage(this));
    }

    public void UpdateStartTime(DateTime newTime)
    {
        _startTime = newTime;

        if (!IsTracking)
        {
            Preferences.Set(AppConstants.PrefIsTracking, true);
            Preferences.Set(AppConstants.PrefStartTime, _startTime);
            UpdateActiveStateTimestamp(); // Record the manual adjustment
            StartTrackingInternal(true);
        }
        else
        {
            Preferences.Set(AppConstants.PrefStartTime, _startTime);
            _notificationService.StartNotification(
                TrackerTitle,
                CurrentStage?.Title ?? "Tracking...",
                _startTime,
                _currentConfig?.DurationType ?? "Time");
            UpdateProgress();
        }
    }

    #endregion
}