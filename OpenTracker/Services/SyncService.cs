using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Java.Net;
using OpenTracker.Models;
using WebDav;

namespace OpenTracker.Services;

public class SyncService
{
    private readonly IDbService _dbService;
    private const string SyncSettingsKey = "SyncSettings";
    private const string RemoteFileName = "opentracker_backup.json";

    public SyncSettings Settings { get; private set; }

    public SyncService(IDbService dbService)
    {
        _dbService = dbService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var json = Preferences.Get(SyncSettingsKey, null);
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
        Preferences.Set(SyncSettingsKey, json);
    }

    public async Task<string> ExportToJsonAsync()
    {
        var backup = new BackupData
        {
            ExportDate = DateTime.UtcNow,
            Manifest = await _dbService.GetManifestAsync(),
            Configs = await _dbService.GetAllConfigsAsync(),
            Sessions = await _dbService.GetAllSessionsAsync()
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
        if (Settings.Target == SyncTarget.None) return;

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
            SaveSettings();
        }
        catch (Exception ex)
        {
            throw new Exception($"Sync failed: {ex.Message}");
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
        var getResponse = await client.GetRawFile(RemoteFileName);

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
        var putResponse = await client.PutFile(RemoteFileName, uploadStream, new PutFileParameters
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
}