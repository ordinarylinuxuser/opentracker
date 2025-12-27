#region

using System.Collections.ObjectModel;
using System.Windows.Input;
using AnyTracker.Models;
using AnyTracker.Pages;
using AnyTracker.Services;
using AnyTracker.Utilities;

#endregion

namespace AnyTracker.ViewModels;

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

    // Preference Keys
    private const string PrefIsTracking = "IsTracking";
    private const string PrefStartTime = "TrackingStartTime";

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
        // Listen for config changes from Settings
        _trackerService.OnTrackerChanged += OnConfigChanged;


        // Check if config is already loaded (from app startup)
        if (_trackerService.CurrentConfig != null) OnConfigChanged();
    }

    #endregion

    #region Binding Properties

    // --- Bindable Properties ---

    public ObservableCollection<TrackingStage> Stages { get; set; } = [];

    // Public accessor for ManualEntryPage
    public DateTime StartTime => _startTime;

    public TrackingStage CurrentStage
    {
        get => _currentStage;
        set
        {
            _currentStage = value;
            OnPropertyChanged();
        }
    }

    public double ElapsedTimeFontSize
    {
        get => _currentConfig?.ElapsedTimeFontSize ?? 36d;
        set
        {
            if (_currentConfig == null) return;
            _currentConfig.ElapsedTimeFontSize = value;
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

    #endregion

    #region Commands

    // --- Commands ---
    public ICommand ToggleTrackingCommand { get; }
    public ICommand EditElapsedTimeCommand { get; }

    #endregion

    #region Methods

    private void OnConfigChanged()
    {
        _currentConfig = _trackerService.CurrentConfig;
        if (_currentConfig == null) return;

        // If we are restoring state, don't reset everything immediately
        var wasTracking = Preferences.Get(PrefIsTracking, false);

        ApplyConfig();

        if (wasTracking) RestoreTrackingState();
    }

    private void ApplyConfig()
    {
        // Don't stop tracking here, just update UI definitions
        Stages.Clear();
        var id = 1;
        foreach (var s in _currentConfig.Stages)
        {
            s.Id = id++;
            Stages.Add(s);
        }

        TrackerTitle = _currentConfig.TrackerName;
        ElapsedTimeFontSize = _currentConfig.ElapsedTimeFontSize;

        // Initial defaults (will be overwritten if RestoreTrackingState is called)
        if (!IsTracking)
        {
            CurrentStage = _currentConfig.StoppedState;
            StartStopButtonText = _currentConfig.ButtonStartText ?? "Start Tracking";
        }
    }

    private void RestoreTrackingState()
    {
        var savedTime = Preferences.Get(PrefStartTime, DateTime.MinValue);
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

    private void StartTracking()
    {
        _startTime = DateTime.Now;

        // Save State
        Preferences.Set(PrefIsTracking, true);
        Preferences.Set(PrefStartTime, _startTime);

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
        UpdateProgress();
    }

    private async void StopTracking()
    {
        if (IsTracking)
        {
            // Save Session
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

        // Clear State
        IsTracking = false;
        Preferences.Set(PrefIsTracking, false);
        Preferences.Remove(PrefStartTime);
        _timer?.Stop();
        _notificationService.CancelNotification();

        // Revert to config fallback
        if (_currentConfig != null)
        {
            CurrentStage = _currentConfig.StoppedState;
            StartStopButtonText = _currentConfig.ButtonStartText ?? "Start Tracking";
        }

        ElapsedTime = "00:00:00";
        foreach (var s in Stages) s.IsActive = false;
    }

    private void UpdateProgress()
    {
        var elapsed = DateTime.Now - _startTime;
        ElapsedTime = FormatHelper.FormatTime(elapsed, _currentConfig.DisplayFormat);

        if (_currentConfig != null)
        {
            ElapsedTimeFontSize = _currentConfig.ElapsedTimeFontSize;

            var totalHours = elapsed.TotalHours;
            var activeStage = Stages.FirstOrDefault(s => totalHours >= s.StartHour && totalHours < s.EndHour);

            // If we are past the last stage, stay on the last stage or define behavior
            if (activeStage == null && Stages.Any() && totalHours > Stages.Last().EndHour) activeStage = Stages.Last();

            if (activeStage != null && CurrentStage != activeStage) CurrentStage = activeStage;

            foreach (var s in Stages) s.IsActive = s == activeStage;
        }

        _notificationService.ShowStickyNotification(TrackerTitle,
            $"{CurrentStage?.Title ?? "Tracking..."} {ElapsedTime}");
    }

    private async Task EditElapsedTime()
    {
        // Open the custom Date/Time Picker Page
        await Shell.Current.Navigation.PushModalAsync(new ManualEntryPage(this));
    }

    public void UpdateStartTime(DateTime newTime)
    {
        _startTime = newTime;

        // If we weren't tracking before, we are now (backdated start)
        if (!IsTracking)
        {
            Preferences.Set(PrefIsTracking, true);
            Preferences.Set(PrefStartTime, _startTime);
            StartTrackingInternal(true);
        }
        else
        {
            // Just update the persistent time and refresh UI immediately
            Preferences.Set(PrefStartTime, _startTime);
            UpdateProgress();
        }
    }

    #endregion
}