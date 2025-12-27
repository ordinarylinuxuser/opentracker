#region

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using AnyTracker.Models;
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
        // Listen for config changes from Settings
        _trackerService.OnTrackerChanged += OnConfigChanged;

        // Load initial state if ready
        if (_trackerService.CurrentConfig != null) OnConfigChanged();
    }

    #endregion

    #region Binding Properties

    // --- Bindable Properties ---

    public ObservableCollection<TrackingStage> Stages { get; set; } = [];

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

    #endregion

    #region Methods

    private void OnConfigChanged()
    {
        _currentConfig = _trackerService.CurrentConfig;
        ApplyConfig();
    }

    private void ApplyConfig()
    {
        StopTracking(); // Reset

        Stages.Clear();
        var id = 1;
        foreach (var s in _currentConfig.Stages)
        {
            s.Id = id++;
            Stages.Add(s);
        }

        TrackerTitle = _currentConfig.TrackerName;
        ElapsedTimeFontSize = _currentConfig.ElapsedTimeFontSize;
        CurrentStage = _currentConfig.StoppedState;
        StartStopButtonText = _currentConfig.ButtonStartText ?? "Start Tracking";
    }

    private async void LoadTrackerConfig(string filename)
    {
        StopTracking(); // Reset state
        try
        {
            _currentConfig = await ResourceHelper.LoadJsonResourceFile<TrackerConfig>(filename);

            // Update UI
            Stages.Clear();
            var id = 1;
            foreach (var s in _currentConfig.Stages)
            {
                s.Id = id++;
                Stages.Add(s);
            }

            TrackerTitle = _currentConfig.TrackerName;
            ElapsedTimeFontSize = _currentConfig.ElapsedTimeFontSize;
            CurrentStage = _currentConfig.StoppedState; // Load fallback UI

            StartStopButtonText = _currentConfig.ButtonStartText ?? "Start Tracking";
        }
        catch (Exception e)
        {
            // Handle error (file not found, etc)
            Debug.WriteLine($"Error loading config: {e.Message}");
        }
    }

    private void ToggleTracking()
    {
        if (IsTracking) StopTracking();
        else StartTracking();
    }

    private void StartTracking()
    {
        IsTracking = true;
        _startTime = DateTime.Now;
        StartStopButtonText = _currentConfig.ButtonStopText ?? "Stop Tracking";

        _timer = Application.Current.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (s, e) => UpdateProgress();
        _timer.Start();
        UpdateProgress();
    }

    private void StopTracking()
    {
        IsTracking = false;
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
        ElapsedTimeFontSize = _currentConfig.ElapsedTimeFontSize;

        // Logic to find active stage based on TotalHours...
        var totalHours = elapsed.TotalHours;
        var activeStage = Stages.FirstOrDefault(s => totalHours >= s.StartHour && totalHours < s.EndHour);

        // (Similar logic to previous code for updating CurrentStage)
        if (activeStage != null && CurrentStage != activeStage) CurrentStage = activeStage;

        // Update notification...
        _notificationService.ShowStickyNotification(TrackerTitle,
            $"{CurrentStage?.Title ?? "Tracking..."} {ElapsedTime}");
    }

    #endregion
}