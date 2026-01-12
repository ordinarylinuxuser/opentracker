#region

using OpenTracker.Constants;

#endregion

namespace OpenTracker.Pages;

public partial class AboutPage
{
    public AboutPage()
    {
        InitializeComponent();
        VersionLabel.Text = $"Version: {AppInfo.Current.VersionString}";
    }

    private async void OpenGithub_Clicked(object sender, EventArgs e)
    {
        // Replace with your actual repo URL
        await Launcher.Default.OpenAsync(AppConstants.GitHubRepoUrl);
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}