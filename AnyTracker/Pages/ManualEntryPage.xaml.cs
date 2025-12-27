#region

using AnyTracker.ViewModels;

#endregion

namespace AnyTracker.Pages;

public partial class ManualEntryPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public ManualEntryPage(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;

        // Initialize pickers
        // If not tracking (MinValue), default to Now.
        var initialTime = _viewModel.StartTime == DateTime.MinValue ? DateTime.Now : _viewModel.StartTime;

        StartDatePicker.Date = initialTime.Date;
        StartTimePicker.Time = initialTime.TimeOfDay;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var date = StartDatePicker.Date;
        var time = StartTimePicker.Time;

        if (date == null || time == null)
        {
            await DisplayAlertAsync("Invalid Input", "Please select both date and time.", "OK");
            return;
        }

        var dateValue = date.Value;
        var timeValue = time.Value;
        var newDateTime = dateValue.Add(timeValue);

        // Prevent setting time in the future
        if (newDateTime > DateTime.Now)
        {
            await DisplayAlertAsync("Invalid Time", "Start time cannot be in the future.", "OK");
            return;
        }

        _viewModel.UpdateStartTime(newDateTime);
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}