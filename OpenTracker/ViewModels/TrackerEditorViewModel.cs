#region

using System.Collections.ObjectModel;
using System.Windows.Input;
using OpenTracker.Models;
using OpenTracker.Pages;
using OpenTracker.Services;

#endregion

namespace OpenTracker.ViewModels;

public class TrackerEditorViewModel : BindableObject
{
    private readonly IDbService _dbService;
    private TrackerConfig _editingConfig;
    private TrackerManifestItem _editingItem;
    private bool _isNew;

    // Form Properties
    public string TrackerName { get; set; }
    public string Icon { get; set; } = "⏱️"; // Default
    public string ButtonStartText { get; set; } = "Start";
    public string ButtonStopText { get; set; } = "Stop";
    public string DisplayFormat { get; set; } = "Time";
    public ObservableCollection<TrackingStage> Stages { get; set; } = new();

    public List<string> DisplayFormats { get; } = new() { "Time", "Days", "Weeks" };

    public ICommand SaveCommand { get; }
    public ICommand AddStageCommand { get; }
    public ICommand EditStageCommand { get; }
    public ICommand DeleteStageCommand { get; }

    public TrackerEditorViewModel(IDbService dbService)
    {
        _dbService = dbService;
        SaveCommand = new Command(async () => await SaveAsync());
        AddStageCommand = new Command(async () => await AddStageAsync());
        EditStageCommand = new Command<TrackingStage>(async (s) => await EditStageAsync(s));
        DeleteStageCommand = new Command<TrackingStage>(DeleteStage);
    }

    public void LoadForEditing(TrackerManifestItem item, TrackerConfig config)
    {
        _isNew = false;
        _editingItem = item;
        _editingConfig = config;

        TrackerName = config.TrackerName;
        Icon = item.Icon;
        ButtonStartText = config.ButtonStartText;
        ButtonStopText = config.ButtonStopText;
        DisplayFormat = config.DisplayFormat;

        Stages.Clear();
        foreach (var s in config.Stages) Stages.Add(s);

        OnPropertyChanged(string.Empty);
    }

    public void InitializeNew()
    {
        _isNew = true;
        // Generate a unique ID for new trackers
        var fileId = $"custom_{Guid.NewGuid()}.json";

        _editingItem = new TrackerManifestItem { FileName = fileId, Name = "", Icon = "" };
        _editingConfig = new TrackerConfig
        {
            FileName = fileId,
            TrackerName = "",
            ButtonStartText = "Start",
            ButtonStopText = "Stop",
            StoppedState = new TrackingStage
            {
                Title = "Idle",
                Description = "Ready to track",
                Icon = "💤",
                ColorHex = "#757575"
            },
            Stages = new List<TrackingStage>()
        };

        TrackerName = "";
        Stages.Clear();
        OnPropertyChanged(string.Empty);
    }

    private async Task AddStageAsync()
    {
        var newStage = new TrackingStage
        {
            Title = "New Stage",
            Description = "Description",
            Icon = "✨",
            ColorHex = "#2196F3",
            StartHour = 0,
            EndHour = 1
        };

        // Open StageEditorPage
        var page = new StageEditorPage(newStage, (s) =>
        {
            // Callback when saved
            Stages.Add(s);
        });

        await Shell.Current.Navigation.PushModalAsync(page);
    }

    private async Task EditStageAsync(TrackingStage stage)
    {
        if (stage == null) return;

        var page = new StageEditorPage(stage, (s) =>
        {
            // Force UI refresh if needed by removing/adding index, or just notify property changed if observable
            // Simple hack to refresh list UI:
            var index = Stages.IndexOf(stage);
            if (index >= 0) Stages[index] = s;
        });

        await Shell.Current.Navigation.PushModalAsync(page);
    }

    private void DeleteStage(TrackingStage stage)
    {
        if (Stages.Contains(stage)) Stages.Remove(stage);
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(TrackerName))
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Tracker Name is required.", "OK");
            return;
        }

        _editingItem.Name = TrackerName;
        _editingItem.Icon = Icon;

        _editingConfig.TrackerName = TrackerName;
        _editingConfig.ButtonStartText = ButtonStartText;
        _editingConfig.ButtonStopText = ButtonStopText;
        _editingConfig.DisplayFormat = DisplayFormat;
        _editingConfig.Stages = Stages.ToList();

        // Ensure stopped state exists
        if (_editingConfig.StoppedState == null)
            _editingConfig.StoppedState = new TrackingStage
            { Title = "Stopped", Description = "Not running", Icon = "zzz", ColorHex = "#555555" };

        await _dbService.SaveTrackerAsync(_editingItem, _editingConfig);
        await Shell.Current.Navigation.PopModalAsync();
    }
}