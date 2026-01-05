using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using OpenTracker.Models;
using OpenTracker.Services;

namespace OpenTracker.ViewModels;

public class ChartBar : BindableObject
{
    public string Label { get; set; }        // "1", "2", "Mon", "Week 1"
    public double HeightRequest { get; set; } // Calculated height
    public string ColorHex { get; set; }     // Bar color
    public string ValueLabel { get; set; }   // "2h 30m"
    public bool HasData { get; set; }        // To toggle visibility or opacity
}

public class HistoryViewModel : BindableObject
{
    private readonly IDbService _dbService;
    private readonly TrackerService _trackerService;
    private bool _isLoading;
    private DateTime _currentMonth;
    private string _monthlySummary = "0 hrs";
    private bool _isWeeklyView = false; // Toggle state

    public HistoryViewModel(IDbService dbService, TrackerService trackerService)
    {
        _dbService = dbService;
        _trackerService = trackerService;

        // Default to current month
        _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        RefreshCommand = new Command(async () => await LoadHistoryAsync());
        PreviousMonthCommand = new Command(async () => await ChangeMonth(-1));
        NextMonthCommand = new Command(async () => await ChangeMonth(1));
        DeleteSessionCommand = new Command<TrackingSession>(async (s) => await DeleteSession(s));
        ToggleChartModeCommand = new Command(() =>
        {
            IsWeeklyView = !IsWeeklyView;
            // Reload chart data without fetching from DB again if possible, 
            // but for simplicity we'll just re-process the list we have.
            if (HistoryList.Count > 0) ProcessChartData(_allSessionsCache);
        });
    }

    private List<TrackingSession> _allSessionsCache = new();

    public ObservableCollection<TrackingSession> HistoryList { get; set; } = new();
    public ObservableCollection<ChartBar> ChartData { get; set; } = new();

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

    public bool IsWeeklyView
    {
        get => _isWeeklyView;
        set
        {
            _isWeeklyView = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DailyButtonColor));
            OnPropertyChanged(nameof(WeeklyButtonColor));
        }
    }

    // Visual toggles for the buttons
    public Color DailyButtonColor => !IsWeeklyView ? Color.FromArgb("#2196F3") : Color.FromArgb("#333333");
    public Color WeeklyButtonColor => IsWeeklyView ? Color.FromArgb("#2196F3") : Color.FromArgb("#333333");

    public ICommand RefreshCommand { get; }
    public ICommand PreviousMonthCommand { get; }
    public ICommand NextMonthCommand { get; }
    public ICommand DeleteSessionCommand { get; }
    public ICommand ToggleChartModeCommand { get; }

    public async Task LoadHistoryAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var currentConfig = _trackerService.CurrentConfig;
            if (currentConfig == null) return;

            // Fetch from DB
            _allSessionsCache = await _dbService.GetHistoryAsync(currentConfig.TrackerName);

            // Filter List for Display (Sessions in this month)
            // Note: A session starting in Prev Month but ending in This Month is currently 
            // shown based on StartTime. Logic can be adjusted if needed.
            var filteredList = _allSessionsCache
                .Where(s => s.StartTime.Year == _currentMonth.Year && s.StartTime.Month == _currentMonth.Month)
                .OrderByDescending(s => s.StartTime)
                .ToList();

            HistoryList.Clear();
            foreach (var s in filteredList) HistoryList.Add(s);

            // Calculate Monthly Total (Sum of list)
            var totalSeconds = filteredList.Sum(s => s.DurationSeconds);
            var span = TimeSpan.FromSeconds(totalSeconds);
            MonthlySummary = $"Total: {Math.Floor(span.TotalHours)}h {span.Minutes}m";

            // Process Chart (Visuals)
            ProcessChartData(_allSessionsCache);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ProcessChartData(List<TrackingSession> sessions)
    {
        ChartData.Clear();

        // 1. Calculate Daily Totals (Splitting across midnights)
        var dailyTotals = new Dictionary<int, double>(); // Key: Day of Month (1-31)

        int daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);

        // Initialize dictionary
        for (int i = 1; i <= daysInMonth; i++) dailyTotals[i] = 0;

        foreach (var session in sessions)
        {
            var current = session.StartTime;
            var end = session.EndTime;

            // Loop while we have duration to process
            while (current < end)
            {
                // Only process if this part of the session is within the currently selected month
                if (current.Year == _currentMonth.Year && current.Month == _currentMonth.Month)
                {
                    var nextMidnight = current.Date.AddDays(1);
                    // Determine end of this segment (either midnight or session end)
                    var segmentEnd = (end < nextMidnight) ? end : nextMidnight;

                    var duration = (segmentEnd - current).TotalSeconds;
                    dailyTotals[current.Day] += duration;
                }

                // Move current to start of next day (or end of session)
                current = current.Date.AddDays(1);
            }
        }

        // 2. Generate Chart Bars based on Mode
        if (IsWeeklyView)
        {
            GenerateWeeklyChart(dailyTotals, daysInMonth);
        }
        else
        {
            GenerateDailyChart(dailyTotals, daysInMonth);
        }
    }

    private void GenerateDailyChart(Dictionary<int, double> dailyTotals, int daysInMonth)
    {
        double maxVal = dailyTotals.Values.DefaultIfEmpty(0).Max();
        if (maxVal <= 0) maxVal = 1; // Avoid divide by zero

        for (int day = 1; day <= daysInMonth; day++)
        {
            var val = dailyTotals[day];
            var height = (val / maxVal) * 150; // 150 is UI height
            if (val > 0 && height < 10) height = 10; // Min height

            ChartData.Add(new ChartBar
            {
                Label = day.ToString(),
                HeightRequest = height,
                ColorHex = val > 0 ? "#2196F3" : "#2D2D2D",
                ValueLabel = val > 0 ? FormatTimeShort(val) : "",
                HasData = val > 0
            });
        }
    }

    private void GenerateWeeklyChart(Dictionary<int, double> dailyTotals, int daysInMonth)
    {
        var weeklyTotals = new Dictionary<int, double>();
        var culture = CultureInfo.CurrentCulture;

        // Group days into weeks
        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(_currentMonth.Year, _currentMonth.Month, day);
            int weekNum = culture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            if (!weeklyTotals.ContainsKey(weekNum)) weeklyTotals[weekNum] = 0;
            weeklyTotals[weekNum] += dailyTotals[day];
        }

        double maxVal = weeklyTotals.Values.DefaultIfEmpty(0).Max();
        if (maxVal <= 0) maxVal = 1;

        int weekIndex = 1;
        foreach (var kvp in weeklyTotals.OrderBy(k => k.Key))
        {
            var val = kvp.Value;
            var height = (val / maxVal) * 150;
            if (val > 0 && height < 10) height = 10;

            ChartData.Add(new ChartBar
            {
                Label = $"W{weekIndex++}",
                HeightRequest = height,
                ColorHex = val > 0 ? "#4CAF50" : "#2D2D2D", // Different color for weeks
                ValueLabel = val > 0 ? FormatTimeShort(val) : "",
                HasData = val > 0
            });
        }
    }

    private string FormatTimeShort(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalHours >= 1) return $"{ts.TotalHours:F1}h";
        return $"{ts.Minutes}m";
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
        bool confirm = await Shell.Current.DisplayAlertAsync("Delete", "Delete this session?", "Yes", "No");
        if (!confirm) return;

        await _dbService.DeleteSessionAsync(session.Id);
        await LoadHistoryAsync();
    }
}