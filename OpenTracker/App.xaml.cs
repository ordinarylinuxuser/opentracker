#region

using OpenTracker.Pages;
using OpenTracker.Services;

#endregion

namespace OpenTracker;

public partial class App : Application
{
    private readonly Page _rootPage;
    private readonly SyncService _syncService;

    public App(LoadingPage loadingPage, SyncService syncService)
    {
        InitializeComponent();
        _rootPage = loadingPage;
        _syncService = syncService;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_rootPage);
    }

    protected override void OnStart()
    {
        // Trigger check immediately on launch
        Task.Run(async () => await _syncService.StartAutoSync());
    }

    protected override void OnSleep()
    {
        // Optional: Stop timer to save resources while backgrounded
        _syncService.StopAutoSync();
    }

    protected override void OnResume()
    {
        // Trigger check immediately when user comes back
        Task.Run(async () => await _syncService.StartAutoSync());
    }
}