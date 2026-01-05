#region

using OpenTracker.Services;

#endregion

namespace OpenTracker.Pages;

public partial class LoadingPage : ContentPage
{
    private readonly TrackerService _trackerService;

    public LoadingPage(TrackerService trackerService)
    {
        InitializeComponent();
        _trackerService = trackerService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Load data
        await _trackerService.InitializeAsync();

        // Artificial delay if needed for UX, or just proceed
        await Task.Delay(500);

        // Switch to Main App Shell in a non-obsolete, null-safe way
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        if (app.Windows?.Count > 0)
        {
            // For single-window apps replace the root page of the first window
            app.Windows[0].Page = new AppShell();
        }
        else
        {
            // No windows yet — open a new window with the AppShell
            app.OpenWindow(new Microsoft.Maui.Controls.Window(new AppShell()));
        }
    }
}