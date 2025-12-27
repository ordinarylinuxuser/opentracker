#region

using AnyTracker.ViewModels;

#endregion

namespace AnyTracker;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}