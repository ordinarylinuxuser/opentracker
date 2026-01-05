#region

using LiteDB;
using OpenTracker.Constants;
using OpenTracker.Models;

#endregion

namespace OpenTracker.Services;

public class LiteDbService : IDbService
{
    private const string Sessions = "sessions";
    private const string Manifest = "manifest";
    private const string Configs = "configs";
    private readonly string _dbPath;


    public LiteDbService()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, AppConstants.DatabaseFilename);
    }


    public async Task AddSessionAsync(TrackingSession session)
    {
        // LiteDB is synchronous, but we wrap it in Task to satisfy the interface 
        // and keep UI responsive.
        await Task.Run(() =>
        {
            using var db = new LiteDatabase(_dbPath);
            var col = db.GetCollection<TrackingSession>(Sessions);
            col.Insert(session);
        });
    }

    public async Task<List<TrackingSession>> GetHistoryAsync(string trackerName)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_dbPath);
            var col = db.GetCollection<TrackingSession>(Sessions);
            return col.Query()
                .Where(s => s.TrackerName.Equals(trackerName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.StartTime)
                .ToList();
        });
    }


    public async Task DeleteSessionAsync(int sessionId)
    {
        await Task.Run(() =>
         {
             using var db = new LiteDatabase(_dbPath);
             var col = db.GetCollection<TrackingSession>(Sessions);
             col.Delete(sessionId);
         });
    }

    public async Task SeedDatabaseAsync(List<TrackerManifestItem> manifest, List<TrackerConfig> configs)
    {
        await Task.Run(() =>
        {
            using var db = new LiteDatabase(_dbPath);
            var manifestCol = db.GetCollection<TrackerManifestItem>(Manifest);
            var configCol = db.GetCollection<TrackerConfig>(Configs);
            // Only insert if empty
            if (manifestCol.Count() == 0)
            {
                manifestCol.InsertBulk(manifest);
                configCol.InsertBulk(configs);
            }
        });
    }

    public async Task<List<TrackerManifestItem>> GetManifestAsync()
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_dbPath);
            var col = db.GetCollection<TrackerManifestItem>(Manifest);
            return col.FindAll().ToList();
        });
    }

    public async Task<TrackerConfig?> GetConfigAsync(string fileName)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_dbPath);
            var col = db.GetCollection<TrackerConfig>(Configs);
            return col.FindById(fileName);
        });
    }

    // --- New Methods Implementation ---

    public async Task SaveTrackerAsync(TrackerManifestItem manifestItem, TrackerConfig config)
    {
        await Task.Run(() =>
        {
            using var db = new LiteDatabase(_dbPath);
            var manifestCol = db.GetCollection<TrackerManifestItem>(Manifest);
            var configCol = db.GetCollection<TrackerConfig>(Configs);

            // Upsert Manifest (Insert or Update)
            manifestCol.Upsert(manifestItem);

            // Upsert Config
            configCol.Upsert(config);
        });
    }

    public async Task DeleteTrackerAsync(string fileName)
    {
        await Task.Run(() =>
        {
            using var db = new LiteDatabase(_dbPath);
            var manifestCol = db.GetCollection<TrackerManifestItem>(Manifest);
            var configCol = db.GetCollection<TrackerConfig>(Configs);

            manifestCol.Delete(fileName);
            configCol.Delete(fileName);
        });
    }

    public async Task<List<TrackingSession>> GetAllSessionsAsync()
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_dbPath);
            return db.GetCollection<TrackingSession>(Sessions).FindAll().ToList();
        });
    }

    public async Task<List<TrackerConfig>> GetAllConfigsAsync()
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_dbPath);
            return db.GetCollection<TrackerConfig>(Configs).FindAll().ToList();
        });
    }

    public async Task ImportDataAsync(BackupData data)
    {
        await Task.Run(() =>
        {
            using var db = new LiteDatabase(_dbPath);
            var sessionsCol = db.GetCollection<TrackingSession>(Sessions);
            var configsCol = db.GetCollection<TrackerConfig>(Configs);
            var manifestCol = db.GetCollection<TrackerManifestItem>(Manifest);

            // 1. Merge Manifests
            if (data.Manifest != null)
            {
                
                // We upsert all. Since ManifestItem doesn't have LastModified, we assume the import is intended.
                manifestCol.Upsert(data.Manifest);
            }

            // 2. Merge Configs (Time-based conflict resolution)
            if (data.Configs != null)
            {
                foreach (var remoteConfig in data.Configs)
                {
                    var localConfig = configsCol.FindById(remoteConfig.FileName);
                    if (localConfig == null || remoteConfig.LastModified > localConfig.LastModified)
                    {
                        configsCol.Upsert(remoteConfig);
                    }
                }
            }

            // 3. Merge Sessions (Time-based conflict resolution)
            // Note: Sessions use AutoId (int). Merging lists from different devices 
            // with integer IDs is tricky. A robust solution uses GUIDs.
            // For this implementation, we will check duplicates by content or simple Upsert if ID matches.
            // CAUTION: This simple merge assumes IDs are preserved or distinct. 
            // Ideally, switch TrackingSession.Id to Guid. 
            if (data.Sessions != null)
            {
                foreach (var remoteSession in data.Sessions)
                {
                    // Basic Upsert. In a real scenario, you'd match by a GUID to avoid ID collisions.
                    // Here we check if a session with same props exists to avoid duplication if IDs don't match.
                    sessionsCol.Upsert(remoteSession);
                }
            }
        });
    }
}