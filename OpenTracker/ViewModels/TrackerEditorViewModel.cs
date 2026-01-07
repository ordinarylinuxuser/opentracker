#region

using OpenTracker.Models;
using OpenTracker.Pages;
using OpenTracker.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

#endregion

namespace OpenTracker.ViewModels;

public class TrackerEditorViewModel : BindableObject
{
    private readonly IDbService _dbService;
    private TrackerConfig _editingConfig;
    private TrackerManifestItem _editingItem;
    private bool _isNew;

    public string TrackerName { get; set; }
    public string Icon { get; set; } = "⏱️";
    public string ButtonStartText { get; set; } = "Start";
    public string ButtonStopText { get; set; } = "Stop";

    public string DurationType { get; set; } = "Time";
    public double CycleLength { get; set; } = 24.0;

    public ObservableCollection<TrackingStage> Stages { get; set; } = new();

    // Changed options
    public List<string> DurationTypes { get; } = new() { "Time", "Day", "Week" };

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
        DurationType = config.DurationType; // Load duration type
        CycleLength = config.CycleLength > 0 ? config.CycleLength : 24.0; // Load cycle length

        Stages.Clear();
        foreach (var s in config.Stages) Stages.Add(s);

        OnPropertyChanged(string.Empty);
    }

    public void InitializeNew()
    {
        _isNew = true;
        var fileId = $"custom_{Guid.NewGuid()}.json";

        _editingItem = new TrackerManifestItem { FileName = fileId, Name = "", Icon = "" };
        _editingConfig = new TrackerConfig
        {
            FileName = fileId,
            TrackerName = "",
            ButtonStartText = "Start",
            ButtonStopText = "Stop",
            CycleLength = 24.0,
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
        DurationType = "Time";
        CycleLength = 24.0;
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
            Start = 0,
            End = 1
        };

        // Pass DurationType to the editor to show correct labels
        var page = new StageEditorPage(newStage, DurationType, (s) =>
        {
            Stages.Add(s);
        });

        await Shell.Current.Navigation.PushModalAsync(page);
    }

    private async Task EditStageAsync(TrackingStage stage)
    {
        if (stage == null) return;

        // Pass DurationType to the editor
        var page = new StageEditorPage(stage, DurationType, (s) =>
        {
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
            await Application.Current.MainPage.DisplayAlertAsync("Error", "Tracker Name is required.", "OK");
            return;
        }

        _editingItem.Name = TrackerName;
        _editingItem.Icon = Icon;

        _editingConfig.TrackerName = TrackerName;
        _editingConfig.ButtonStartText = ButtonStartText;
        _editingConfig.ButtonStopText = ButtonStopText;
        _editingConfig.DurationType = DurationType;
        _editingConfig.CycleLength = CycleLength;
        _editingConfig.Stages = Stages.ToList();
        int i = 1;
        _editingConfig.Stages.ForEach(s => s.Id = i++);

        _editingConfig.StoppedState ??= new TrackingStage { Title = "Stopped", Description = "Not running", Icon = "zzz", ColorHex = "#555555" };

        await _dbService.SaveTrackerAsync(_editingItem, _editingConfig);
        await Shell.Current.Navigation.PopModalAsync();
    }
}