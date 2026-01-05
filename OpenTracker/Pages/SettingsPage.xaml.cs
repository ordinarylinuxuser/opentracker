using OpenTracker.Models;
using OpenTracker.Services;

namespace OpenTracker.Pages;

public partial class SettingsPage
{
    private readonly TrackerService _trackerService;
    private readonly SyncService _syncService;

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
        // Populate Pickers
        SyncTargetPicker.ItemsSource = Enum.GetNames(typeof(SyncTarget));
        IntervalPicker.ItemsSource = Enum.GetNames(typeof(SyncInterval));

        // Load Settings
        var settings = _syncService.Settings;
        SyncTargetPicker.SelectedIndex = (int)settings.Target;
        IntervalPicker.SelectedIndex = (int)settings.Interval;
        HostEntry.Text = settings.HostUrl;
        UserEntry.Text = settings.Username;
        PassEntry.Text = settings.Password;

        if (settings.LastSyncTime != DateTime.MinValue)
            LastSyncLabel.Text = $"Last Sync: {settings.LastSyncTime:g}";

        UpdateSyncVisibility();
    }

    private void OnSyncTargetChanged(object sender, EventArgs e)
    {
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

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            var json = await _syncService.ExportToJsonAsync();

            // For now, copy to clipboard or save to a shared file
            var fileName = $"backup_{DateTime.Now:yyyyMMdd_HHmm}.json";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, json);

            // Share the file
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "OpenTracker Backup",
                File = new ShareFile(filePath)
            });
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
}