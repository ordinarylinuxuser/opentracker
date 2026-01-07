#region

using OpenTracker.Constants;
using OpenTracker.Models;
using OpenTracker.Utilities;
using System.Diagnostics;

#endregion

namespace OpenTracker.Services;

public class TrackerService
{
    private readonly IDbService _dbService;

    public TrackerConfig CurrentConfig { get; private set; }
    public List<TrackerManifestItem> Manifest { get; private set; } = [];

    public event Action OnTrackerChanged;

    public TrackerService(IDbService dbService)
    {
        _dbService = dbService;
    }

    public async Task InitializeAsync()
    {
        try
        {
            // 1. Check if DB is initialized
            var existingManifest = await _dbService.GetManifestAsync();

            if (existingManifest == null || existingManifest.Count == 0)
            {
                // Load from Resources
                var rawManifest =
                    await ResourceHelper.LoadJsonResourceFile<List<TrackerManifestItem>>(AppConstants
                        .TrackerManifestFile);
                var rawConfigs = new List<TrackerConfig>();

                foreach (var item in rawManifest)
                    try
                    {
                        var config = await ResourceHelper.LoadJsonResourceFile<TrackerConfig>(item.FileName);
                        // Link the config to the manifest item via FileName
                        config.FileName = item.FileName;
                        config.TrackerName = item.Name;
                        // Assign IDs to stages
                        int id = 1;
                        config.Stages.ForEach(stage =>
                        {
                            stage.Id = id++;
                        });
                        rawConfigs.Add(config);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to load resource {item.FileName}: {ex.Message}");
                    }

                // Save to DB
                await _dbService.SeedDatabaseAsync(rawManifest, rawConfigs);
                Manifest = rawManifest;
            }
            else
            {
                Manifest = existingManifest;
            }

            // 2. Load Config (Last used or Default)
            var lastConfig = Preferences.Get(AppConstants.PrefLastConfig, AppConstants.DefaultTrackerFile);
            await LoadTrackerConfigAsync(lastConfig);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error initializing tracker service: {ex.Message}");
        }
    }

    public async Task LoadTrackerConfigAsync(string filename)
    {
        if (CurrentConfig != null && CurrentConfig.FileName == filename) return;
        try
        {
            CurrentConfig = await _dbService.GetConfigAsync(filename);

            // Fallback if DB fetch fails (shouldn't happen after seed)
            if (CurrentConfig == null)
            {
                Debug.WriteLine($"Config {filename} not found in DB, attempting resource load.");
                CurrentConfig = await ResourceHelper.LoadJsonResourceFile<TrackerConfig>(filename);
            }

            // Save preference
            Preferences.Set(AppConstants.PrefLastConfig, filename);
            Preferences.Set(AppConstants.PrefActiveStateModified, DateTime.UtcNow.ToString("o"));

            OnTrackerChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading config {filename}: {ex.Message}");
        }
    }
}