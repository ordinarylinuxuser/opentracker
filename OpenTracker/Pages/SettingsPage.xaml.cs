using OpenTracker.Models;
using OpenTracker.Services;

namespace OpenTracker.Pages;

public partial class SettingsPage
{
    private readonly TrackerService _trackerService;
    private readonly SyncService _syncService;

    private bool _isInitializing; // Add this flag

    public SettingsPage(TrackerService trackerService, SyncService syncService)
    {
        InitializeComponent();
        _trackerService = trackerService;
        _syncService = syncService;

        UpdateLabel();
        _trackerService.OnTrackerChanged += UpdateLabel;

        InitializeSyncUI();
    }

    private void UpdateLabel()
    {
        if (_trackerService.CurrentConfig != null)
            CurrentTrackerLabel.Text = _trackerService.CurrentConfig.TrackerName;
    }

    private void InitializeSyncUI()
    {
        _isInitializing = true; // Block events
        // Populate Pickers
        SyncTargetPicker.ItemsSource = Enum.GetNames(typeof(SyncTarget));
        IntervalPicker.ItemsSource = Enum.GetNames(typeof(SyncInterval));

        // Load Settings
        var settings = _syncService.Settings;
        HostEntry.Text = settings.HostUrl;
        UserEntry.Text = settings.Username;
        PassEntry.Text = settings.Password;
        SyncTargetPicker.SelectedIndex = (int)settings.Target;
        IntervalPicker.SelectedIndex = (int)settings.Interval;

        if (settings.LastSyncTime != DateTime.MinValue)
            LastSyncLabel.Text = $"Last Sync: {settings.LastSyncTime:g}";

        UpdateSyncVisibility();
        _isInitializing = false; // Enable events
    }

    private void OnSyncTargetChanged(object sender, EventArgs e)
    {
        if (_isInitializing) return; // Skip logic during init
        UpdateSyncVisibility();
        SaveSyncSettings();
    }

    private void UpdateSyncVisibility()
    {
        bool isEnabled = SyncTargetPicker.SelectedIndex > 0; // 0 is None
        SyncConfigLayout.IsVisible = isEnabled;
    }

    private void SaveSyncSettings()
    {
        if (SyncTargetPicker.SelectedIndex >= 0)
            _syncService.Settings.Target = (SyncTarget)SyncTargetPicker.SelectedIndex;

        if (IntervalPicker.SelectedIndex >= 0)
            _syncService.Settings.Interval = (SyncInterval)IntervalPicker.SelectedIndex;

        _syncService.Settings.HostUrl = HostEntry.Text;
        _syncService.Settings.Username = UserEntry.Text;
        _syncService.Settings.Password = PassEntry.Text;

        _syncService.SaveSettings();
    }

    private async void OnSyncNowClicked(object sender, EventArgs e)
    {
        SaveSyncSettings(); // Save current inputs first
        try
        {
            await _syncService.SyncNowAsync();
            LastSyncLabel.Text = $"Last Sync: {DateTime.Now:g}";
            await DisplayAlertAsync("Success", "Synchronization completed.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Sync Error", ex.Message, "OK");
        }
    }

    // Existing OnExportClicked modified to use the helper (optional refactor)
    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            var json = await _syncService.ExportToJsonAsync();
            var fileName = $"backup_{DateTime.Now:yyyyMMdd_HHmm}.json";
            await ShareFileAsync(json, fileName, "OpenTracker Full Backup");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Export Error", ex.Message, "OK");
        }
    }

    private async void OnImportClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Backup JSON"
            });

            if (result != null)
            {
                using var stream = await result.OpenReadAsync();
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();

                bool confirm = await DisplayAlertAsync("Import Data",
                    "This will merge the imported data with your current data. Continue?", "Yes", "No");

                if (confirm)
                {
                    await _syncService.ImportFromJsonAsync(json);
                    await DisplayAlertAsync("Success", "Data imported successfully. Please restart the app or reload trackers.", "OK");

                    // Reload manifest in background
                    await _trackerService.InitializeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Import Error", ex.Message, "OK");
        }
    }

    private async void OnChangeTrackerTapped(object sender, EventArgs e)
    {
        var selectorPage = new TrackerSelectorPage(_trackerService);
        selectorPage.OnTrackerSelected += async item =>
        {
            if (item.Name != _trackerService.CurrentConfig.TrackerName)
                await _trackerService.LoadTrackerConfigAsync(item.FileName);
        };
        await Navigation.PushModalAsync(selectorPage);
    }

    private async void OnAboutTapped(object sender, EventArgs e)
    {
        await Navigation.PushModalAsync(new AboutPage());
    }

    // Helper method to reduce code duplication
    private async Task ShareFileAsync(string content, string fileName, string title)
    {
        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(filePath, content);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(filePath)
        });
    }

    private async void OnExportHistoryJsonClicked(object sender, EventArgs e)
    {
        try
        {
            var json = await _syncService.ExportHistoryToJsonAsync();
            var fileName = $"history_{DateTime.Now:yyyyMMdd_HHmm}.json";
            await ShareFileAsync(json, fileName, "OpenTracker History (JSON)");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Export Error", ex.Message, "OK");
        }
    }

    private async void OnExportHistoryCsvClicked(object sender, EventArgs e)
    {
        try
        {
            var csv = await _syncService.ExportHistoryToCsvAsync();
            var fileName = $"history_{DateTime.Now:yyyyMMdd_HHmm}.csv";
            await ShareFileAsync(csv, fileName, "OpenTracker History (CSV)");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Export Error", ex.Message, "OK");
        }
    }
}