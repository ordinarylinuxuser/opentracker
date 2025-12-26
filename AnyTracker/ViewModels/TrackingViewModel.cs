using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Input;
using AnyTracker.Models;
using AnyTracker.Pages;
using AnyTracker.Services;
using AnyTracker.Utilities;

namespace AnyTracker.ViewModels;

public class TrackingViewModel : BindableObject
{
    private readonly INotificationService _notificationService;
    private TrackerConfig _currentConfig;

    private TrackingStage _currentStage;

    private string _elapsedTime = "00:00:00";

    private bool _isTracking;

    private string _startStopButtonText;
    private DateTime _startTime;

    private IDispatcherTimer _timer;

    private string _trackerTitle;

    public TrackingViewModel(INotificationService notificationService)
    {
        _notificationService = notificationService;
        ToggleTrackingCommand = new Command(ToggleTracking);
        OpenSettingsCommand = new Command(OpenSettings);

        // Load default config
        LoadTrackerConfig("config_fasting.json");
    }

    // --- Bindable Properties ---

    public ObservableCollection<TrackingStage> Stages { get; set; } = new();

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

    // --- Commands ---
    public ICommand ToggleTrackingCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    public async void LoadTrackerConfig(string filename)
    {
        StopTracking(); // Reset state
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(filename);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync() ?? throw new NullReferenceException();
            _currentConfig = JsonSerializer.Deserialize<TrackerConfig>(json) ?? throw new NullReferenceException();

            // Update UI
            Stages.Clear();
            foreach (var s in _currentConfig.Stages) Stages.Add(s);

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

        var progress = _currentStage.Id * 100 / Stages.Count;

        // Update notification...
        _notificationService.ShowStickyNotification(TrackerTitle, CurrentStage?.Title ?? "Tracking...", progress);
    }

    // --- Navigation Logic ---

    private async void OpenSettings()
    {
        // We pass the current title and a callback function(LoadTrackerConfig)
        // This acts as the bridge between Settings and Main Page
        var settingsPage = new SettingsPage(TrackerTitle, fileName => { LoadTrackerConfig(fileName); });

        // Use PushAsync for a "Page" transition, or PushModalAsync for a "Popup" feel
        // Settings are usually a PushAsync (slide in from right)
        await Application.Current.MainPage.Navigation.PushAsync(settingsPage);
    }
}