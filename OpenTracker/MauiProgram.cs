#region

using DotNet.Meteor.HotReload.Plugin;
using Microsoft.Extensions.Logging;
using OpenTracker.Pages;
using OpenTracker.Services;
using OpenTracker.ViewModels;

#endregion

namespace OpenTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
#if DEBUG
            // .EnableHotReload()
#endif
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

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