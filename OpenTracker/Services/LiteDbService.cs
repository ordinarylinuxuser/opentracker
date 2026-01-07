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
                .Where(s => s.TrackerName.Equals(trackerName, StringComparison.OrdinalIgnoreCase) && !s.IsDeleted)
                .OrderByDescending(x => x.StartTime)
                .ToList();
        });
    }


    public async Task DeleteSessionAsync(Guid sessionId)
    {
        await Task.Run(() =>
         {
             using var db = new LiteDatabase(_dbPath);
             var col = db.GetCollection<TrackingSession>(Sessions);
             var session = col.FindById(sessionId);
             if (session != null)
             {
                 // Soft Delete
                 session.IsDeleted = true;
                 session.LastModified = DateTime.UtcNow;
                 col.Update(session);
             }
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
            return col.FindAll().Where(x => !x.IsDeleted).ToList();
        });
    }

    public async Task<TrackerConfig?> GetConfigAsync(string fileName)
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_dbPath);
            var col = db.GetCollection<TrackerConfig>(Configs);
            var config = col.FindById(fileName);
            // Return null if soft deleted
            return (config != null && !config.IsDeleted) ? config : null;
        });
    }

    // --- New Methods Implementation ---

    public async Task SaveTrackerAsync(TrackerManifestItem manifestItem, TrackerConfig config)
    {
        var now = DateTime.UtcNow;
        manifestItem.LastModified = now;
        manifestItem.IsDeleted = false; // Ensure it's active

        config.LastModified = now;
        config.IsDeleted = false; // Ensure it's active

        await Task.Run(() =>
        {
            using var db = new LiteDatabase(_dbPath);
            var manifestCol = db.GetCollection<TrackerManifestItem>(Manifest);
            var configCol = db.GetCollection<TrackerConfig>(Configs);

            manifestCol.Upsert(manifestItem);
            configCol.Upsert(config);
        });
    }

    public async Task DeleteTrackerAsync(string fileName)
    {
        var now = DateTime.UtcNow;
        await Task.Run(() =>
        {
            using var db = new LiteDatabase(_dbPath);
            var manifestCol = db.GetCollection<TrackerManifestItem>(Manifest);
            var configCol = db.GetCollection<TrackerConfig>(Configs);

            // Soft Delete Manifest
            var manifest = manifestCol.FindById(fileName);
            if (manifest != null)
            {
                manifest.IsDeleted = true;
                manifest.LastModified = now;
                manifestCol.Update(manifest);
            }

            // Soft Delete Config
            var config = configCol.FindById(fileName);
            if (config != null)
            {
                config.IsDeleted = true;
                config.LastModified = now;
                configCol.Update(config);
            }
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

    public async Task<List<TrackerManifestItem>> GetAllManifestsAsync()
    {
        return await Task.Run(() =>
        {
            using var db = new LiteDatabase(_dbPath);
            return db.GetCollection<TrackerManifestItem>(Manifest).FindAll().ToList();
        });
    }

    public async Task ImportDataAsync(BackupData data)
    {
        await Task.Run(() =>
         {
             using var db = new LiteDatabase(_dbPath);

             // 1. Manifests
             if (data.Manifest != null)
             {
                 var col = db.GetCollection<TrackerManifestItem>(Manifest);
                 foreach (var remoteItem in data.Manifest)
                 {
                     var localItem = col.FindById(remoteItem.FileName);
                     if (localItem == null || remoteItem.LastModified > localItem.LastModified)
                     {
                         col.Upsert(remoteItem);
                     }
                 }
             }

             // 2. Configs
             if (data.Configs != null)
             {
                 var col = db.GetCollection<TrackerConfig>(Configs);
                 foreach (var remoteItem in data.Configs)
                 {
                     var localItem = col.FindById(remoteItem.FileName);
                     if (localItem == null || remoteItem.LastModified > localItem.LastModified)
                     {
                         col.Upsert(remoteItem);
                     }
                 }
             }

             // 3. Sessions
             if (data.Sessions != null)
             {
                 var col = db.GetCollection<TrackingSession>(Sessions);
                 foreach (var remoteItem in data.Sessions)
                 {
                     var localItem = col.FindById(remoteItem.Id);

                     if (localItem == null || remoteItem.LastModified > localItem.LastModified)
                     {
                         col.Upsert(remoteItem);
                     }
                 }
             }
         });
    }
}