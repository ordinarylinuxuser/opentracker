using System.Collections.ObjectModel;
using System.Windows.Input;
using OpenTracker.Models;
using OpenTracker.Services;

namespace OpenTracker.ViewModels;


public class HistoryViewModel : BindableObject
{
    private readonly IDbService _dbService;
    private readonly TrackerService _trackerService;
    private bool _isLoading;
    private DateTime _currentMonth;
    private string _monthlySummary = "0 hrs";

    public HistoryViewModel(IDbService dbService, TrackerService trackerService)
    {
        _dbService = dbService;
        _trackerService = trackerService;

        // Default to current month
        _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        // Commands
        RefreshCommand = new Command(async () => await LoadHistoryAsync());
        PreviousMonthCommand = new Command(async () => await ChangeMonth(-1));
        NextMonthCommand = new Command(async () => await ChangeMonth(1));
        DeleteSessionCommand = new Command<TrackingSession>(async (s) => await DeleteSession(s));
    }

    // Collections
    public ObservableCollection<TrackingSession> HistoryList { get; set; } = new();

    // Properties
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public string MonthLabel => _currentMonth.ToString("MMMM yyyy");

    public string MonthlySummary
    {
        get => _monthlySummary;
        set { _monthlySummary = value; OnPropertyChanged(); }
    }

    // Commands
    public ICommand RefreshCommand { get; }
    public ICommand PreviousMonthCommand { get; }
    public ICommand NextMonthCommand { get; }
    public ICommand DeleteSessionCommand { get; }

    public async Task LoadHistoryAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var currentConfig = _trackerService.CurrentConfig;
            if (currentConfig == null) return;

            // Get all sessions for this tracker
            var allSessions = await _dbService.GetHistoryAsync(currentConfig.TrackerName);

            // Filter by current month
            var filtered = allSessions
                .Where(s => s.StartTime.Year == _currentMonth.Year && s.StartTime.Month == _currentMonth.Month)
                .OrderByDescending(s => s.StartTime)
                .ToList();

            HistoryList.Clear();
            double totalSeconds = 0;

            foreach (var s in filtered)
            {
                HistoryList.Add(s);
                totalSeconds += s.DurationSeconds;
            }

            // Calculate Summary
            var span = TimeSpan.FromSeconds(totalSeconds);
            MonthlySummary = $"Total: {Math.Floor(span.TotalHours)}h {span.Minutes}m";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ChangeMonth(int monthsToAdd)
    {
        _currentMonth = _currentMonth.AddMonths(monthsToAdd);
        OnPropertyChanged(nameof(MonthLabel));
        await LoadHistoryAsync();
    }

    private async Task DeleteSession(TrackingSession session)
    {
        if (session == null) return;

        bool confirm = await Shell.Current.DisplayAlertAsync("Delete Session",
            "Are you sure you want to delete this entry?", "Yes", "No");

        if (!confirm) return;

        await _dbService.DeleteSessionAsync(session.Id);
        await LoadHistoryAsync(); // Reload list
    }
}