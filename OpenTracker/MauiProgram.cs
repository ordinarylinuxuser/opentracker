
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using OpenTracker.Pages;
using OpenTracker.Services;
using OpenTracker.ViewModels;

namespace OpenTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if WINDOWS

        // --- ADD THIS BLOCK ---
        builder.ConfigureLifecycleEvents(events =>
    {
        events.AddWindows(wndLifeCycleBuilder =>
        {
            wndLifeCycleBuilder.OnWindowCreated(window =>
            {
                // 1. Get the Window Handle
                var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);

                // 2. Set Fixed Size (e.g. 450x800 for a phone-like look)
                appWindow.Resize(new Windows.Graphics.SizeInt32(450, 1000));

                // 3. Disable Resizing and Maximizing to enforce the "App" feel
                var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                if (presenter != null)
                {
                    presenter.IsMaximizable = true;
                    presenter.IsResizable = true;
                }
            });
        });
    });
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif
        // Register Platform Specific Service
#if ANDROID
        builder.Services
            .AddSingleton<INotificationService, AndroidNotificationService>();
#else
        // Dummy implementation for iOS/Windows to prevent crashes
        builder.Services.AddSingleton<INotificationService>(new MockNotificationService());
#endif
        // Services
        builder.Services.AddSingleton<IDbService, LiteDbService>();
        builder.Services.AddSingleton<TrackerService>();
        builder.Services.AddSingleton<SyncService>();

        // ViewModels
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<HistoryViewModel>();
        builder.Services.AddTransient<TrackerEditorViewModel>();

        // Pages
        builder.Services.AddSingleton<LoadingPage>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<HistoryPage>();

        builder.Services.AddTransient<TrackerEditorPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<TrackerSelectorPage>();
        builder.Services.AddTransient<ManualEntryPage>();
        builder.Services.AddTransient<AboutPage>();

        return builder.Build();
    }
}