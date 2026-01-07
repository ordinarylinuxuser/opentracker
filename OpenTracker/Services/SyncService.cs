using OpenTracker.Constants;
using OpenTracker.Models;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using WebDav;
using Timer = System.Timers.Timer;

namespace OpenTracker.Services;

public class SyncService
{
    private readonly IDbService _dbService;


    // Automation Fields
    private Timer _autoSyncTimer;
    private bool _isSyncing;

    public SyncSettings Settings { get; private set; }

    public SyncService(IDbService dbService)
    {
        _dbService = dbService;
        LoadSettings();
        InitializeAutoSync();
    }

    private void InitializeAutoSync()
    {
        // Check every 60 seconds if it's time to sync
        _autoSyncTimer = new Timer(60000);
        _autoSyncTimer.Elapsed += async (s, e) => await CheckAndSyncIfNeeded();
        _autoSyncTimer.AutoReset = true;
    }

    // Call this from App.xaml.cs OnStart/OnResume
    public async Task StartAutoSync()
    {
        if (Settings.Interval == SyncInterval.Manual || Settings.Target == SyncTarget.None)
            return;

        // 1. Start the periodic timer
        if (!_autoSyncTimer.Enabled)
            _autoSyncTimer.Start();

        // 2. Run an immediate check (e.g. app just opened after 2 days)
        await CheckAndSyncIfNeeded();
    }

    public void StopAutoSync()
    {
        _autoSyncTimer.Stop();
    }

    private async Task CheckAndSyncIfNeeded()
    {
        if (_isSyncing || Settings.Target == SyncTarget.None || Settings.Interval == SyncInterval.Manual)
            return;

        var timeSinceLastSync = DateTime.Now - Settings.LastSyncTime;
        bool shouldSync = false;

        switch (Settings.Interval)
        {
            case SyncInterval.Hourly:
                shouldSync = timeSinceLastSync.TotalHours >= 1;
                break;
            case SyncInterval.Daily:
                shouldSync = timeSinceLastSync.TotalDays >= 1;
                break;
            case SyncInterval.Weekly:
                shouldSync = timeSinceLastSync.TotalDays >= 7;
                break;
        }

        if (shouldSync)
        {
            Console.WriteLine($"Auto-Sync Triggered. Last Sync was: {Settings.LastSyncTime}");
            try
            {
                await SyncNowAsync();
            }
            catch (Exception ex)
            {
                // Silently log error for auto-sync so we don't crash or popup alerts in background
                Console.WriteLine($"Auto-Sync Failed: {ex.Message}");
            }
        }
    }

    private void LoadSettings()
    {
        var json = Preferences.Get(AppConstants.SyncSettingsKey, null);
        if (string.IsNullOrEmpty(json))
        {
            Settings = new SyncSettings { Target = SyncTarget.None, Interval = SyncInterval.Manual };
        }
        else
        {
            Settings = JsonSerializer.Deserialize<SyncSettings>(json);
        }
    }

    public void SaveSettings()
    {
        var json = JsonSerializer.Serialize(Settings);
        Preferences.Set(AppConstants.SyncSettingsKey, json);

        // Re-evaluate automation state
        if (Settings.Interval != SyncInterval.Manual && Settings.Target != SyncTarget.None)
        {
            if (!_autoSyncTimer.Enabled) _autoSyncTimer.Start();
        }
        else
        {
            if (_autoSyncTimer.Enabled) _autoSyncTimer.Stop();
        }
    }

    public async Task<string> ExportToJsonAsync()
    {
        // 1. Capture the local modified time, defaulting to MinValue if never set
        var localModifiedStr = Preferences.Get(AppConstants.PrefActiveStateModified, DateTime.MinValue.ToString("O"));
        DateTime.TryParse(localModifiedStr, out var localModified);

        var activeState = new ActiveTrackingState
        {
            IsTracking = Preferences.Get(AppConstants.PrefIsTracking, false),
            StartTime = Preferences.Get(AppConstants.PrefStartTime, DateTime.MinValue),
            SelectedTrackerFileName = Preferences.Get(AppConstants.PrefLastConfig, "config_fasting.json"),
            LastModified = localModified // Send our local timestamp
        };

        var backup = new BackupData
        {
            ExportDate = DateTime.UtcNow,
            Manifest = await _dbService.GetManifestAsync(),
            Configs = await _dbService.GetAllConfigsAsync(),
            Sessions = await _dbService.GetAllSessionsAsync(),
            ActiveState = activeState
        };

        return JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task ImportFromJsonAsync(string json)
    {
        try
        {
            var backup = JsonSerializer.Deserialize<BackupData>(json);
            if (backup != null)
            {
                await _dbService.ImportDataAsync(backup);

                // 2. Resolve Active State Conflicts
                if (backup.ActiveState != null)
                {
                    var localModifiedStr = Preferences.Get(AppConstants.PrefActiveStateModified, DateTime.MinValue.ToString("O"));
                    DateTime.TryParse(localModifiedStr, out var localModified);

                    // Only apply remote state if the remote timestamp is strictly newer
                    if (backup.ActiveState.LastModified > localModified)
                    {
                        Preferences.Set(AppConstants.PrefIsTracking, backup.ActiveState.IsTracking);
                        Preferences.Set(AppConstants.PrefStartTime, backup.ActiveState.StartTime);
                        Preferences.Set(AppConstants.PrefLastConfig, backup.ActiveState.SelectedTrackerFileName);
                        Preferences.Set(AppConstants.PrefActiveStateModified, backup.ActiveState.LastModified.ToString("O"));

                        // NOTE: After this, the app should ideally reload the MainPage 
                        // to reflect the new tracking state or config.
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Import failed: {ex.Message}");
            throw;
        }
    }

    public async Task SyncNowAsync()
    {
        if (_isSyncing || Settings.Target == SyncTarget.None) return;

        _isSyncing = true; // Lock

        try
        {
            switch (Settings.Target)
            {
                case SyncTarget.WebDav:
                    await SyncWebDav();
                    break;
                case SyncTarget.S3:
                    // await SyncS3();
                    break;
                case SyncTarget.SFTP:
                    // await SyncSFTP();
                    break;
            }

            Settings.LastSyncTime = DateTime.Now;
            // Don't call SaveSettings() here to avoid recursion loop with timer logic
            // Just save the preference string manually or create a specific internal save method
            var json = JsonSerializer.Serialize(Settings);
            Preferences.Set(AppConstants.SyncSettingsKey, json);
        }
        catch (Exception ex)
        {
            throw new Exception($"Sync failed: {ex.Message}");
        }
        finally
        {
            _isSyncing = false; // Unlock
        }
    }

    private async Task SyncWebDav()
    {
        if (string.IsNullOrEmpty(Settings.HostUrl))
            throw new ArgumentException("Host URL is required for WebDAV.");

        var base64Creds = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Settings.Username}:{Settings.Password}"));

        // 1. Setup HttpClient with Pre-emptive Basic Auth
        // This forces credentials to be sent with EVERY request, bypassing the 401 handshake issues.
        using var client = new WebDavClient(new HttpClient
        {
            BaseAddress = new Uri(EnsureTrailingSlash(Settings.HostUrl)),
            DefaultRequestHeaders =
            {
                Authorization = new AuthenticationHeaderValue("Basic", base64Creds)
            }
        });


        // 2. Try Download Remote File (Merge Strategy)
        // We download first to ensure we don't overwrite remote changes with older local data.
        var getResponse = await client.GetRawFile(AppConstants.RemoteFileName);

        if (getResponse.IsSuccessful && getResponse.Stream != null)
        {
            using var reader = new StreamReader(getResponse.Stream);
            var remoteJson = await reader.ReadToEndAsync();


            // This merges remote sessions/configs into our local DB
            await ImportFromJsonAsync(remoteJson);
        }
        else if (getResponse.StatusCode != 404)
        {
            // If it's 404, the file doesn't exist yet, which is fine (first sync).
            // If it's another error (401, 500), throw.
            throw new Exception($"WebDAV Download Failed: {getResponse.StatusCode} {getResponse.Description}");
        }

        // 3. Generate Fresh Export (Now contains merged data)
        var mergedJson = await ExportToJsonAsync();

        // 4. Upload Merged Data
        using var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(mergedJson));
        var putResponse = await client.PutFile(AppConstants.RemoteFileName, uploadStream, new PutFileParameters
        {
            ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json)
        });

        if (!putResponse.IsSuccessful)
        {
            throw new Exception($"WebDAV Upload Failed: {putResponse.StatusCode} {putResponse.Description}");
        }
    }

    private string EnsureTrailingSlash(string url)
    {
        return url.EndsWith("/") ? url : url + "/";
    }

    public void Dispose()
    {
        _autoSyncTimer?.Dispose();
    }

    public async Task<string> ExportHistoryToJsonAsync()
    {
        var sessions = await _dbService.GetAllSessionsAsync();
        return JsonSerializer.Serialize(sessions, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> ExportHistoryToCsvAsync()
    {
        var sessions = await _dbService.GetAllSessionsAsync();
        var sb = new StringBuilder();

        // CSV Header
        sb.AppendLine("Id,TrackerName,StartTime,EndTime,DurationSeconds,DurationDisplay");

        foreach (var s in sessions)
        {
            // Escape TrackerName in case it contains commas
            var name = s.TrackerName.Replace("\"", "\"\"");
            if (name.Contains(',') || name.Contains('\n'))
            {
                name = $"\"{name}\"";
            }

            sb.AppendLine($"{s.Id},{name},{s.StartTime:O},{s.EndTime:O},{s.DurationSeconds},{s.DurationDisplay}");
        }

        return sb.ToString();
    }
}