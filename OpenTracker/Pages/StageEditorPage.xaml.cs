#region

using OpenTracker.Models;

#endregion

namespace OpenTracker.Pages;

public partial class StageEditorPage : ContentPage
{
    private TrackingStage _stage;
    private Action<TrackingStage> _onSave;
    private string _selectedColor = "#FFFFFF";
    private string _durationType;

    public List<string> Emojis { get; } = new()
    {
        "🔥", "⚡", "💤", "🍽️", "🧠", "🏋️", "🧘", "💧", "☕", "🍺", "🚫", "✅", "👶", "💊", "📚", "🌡️", "🚀", "🚨"
    };

    public StageEditorPage(TrackingStage stage, string durationType, Action<TrackingStage> onSave)
    {
        InitializeComponent();
        _stage = stage;
        _durationType = durationType;
        _onSave = onSave;

        BindingContext = this;

        UpdateLabels();
        LoadColors();
        LoadStageData();
    }

    private void UpdateLabels()
    {
        string labelSuffix = "Hour";
        if (_durationType.Equals("Day", StringComparison.OrdinalIgnoreCase)) labelSuffix = "Day";
        if (_durationType.Equals("Week", StringComparison.OrdinalIgnoreCase)) labelSuffix = "Week";

        StartLabel.Text = $"Start {labelSuffix}";
        EndLabel.Text = $"End {labelSuffix}";
    }

    private void LoadColors()
    {
        var colors = new[]
        {
            "#F44336", "#E91E63", "#9C27B0", "#673AB7", "#3F51B5", "#2196F3", "#03A9F4", "#00BCD4", "#009688",
            "#4CAF50", "#8BC34A", "#CDDC39", "#FFEB3B", "#FFC107", "#FF9800", "#FF5722"
        };

        foreach (var c in colors)
        {
            var btn = new Button
            {
                BackgroundColor = Color.FromArgb(c),
                WidthRequest = 40,
                HeightRequest = 40,
                CornerRadius = 20,
                Margin = 2,
                BorderColor = Colors.White,
                BorderWidth = 0
            };
            btn.Clicked += (s, e) =>
            {
                _selectedColor = c;
                TitleEntry.TextColor = Color.FromArgb(c);
            };
            ColorGrid.Children.Add(btn);
        }
    }

    private void LoadStageData()
    {
        TitleEntry.Text = _stage.Title;
        DescEntry.Text = _stage.Description;
        StartEntry.Text = _stage.Start.ToString();
        EndEntry.Text = _stage.End.ToString();
        EmojiEntry.Text = _stage.Icon;
        _selectedColor = _stage.ColorHex;
    }

    private void OnEmojiClicked(object sender, EventArgs e)
    {
        if (sender is Button btn) EmojiEntry.Text = btn.Text;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        _stage.Title = TitleEntry.Text;
        _stage.Description = DescEntry.Text;
        _stage.Icon = EmojiEntry.Text;
        _stage.ColorHex = _selectedColor;

        if (double.TryParse(StartEntry.Text, out double start)) _stage.Start = start;
        if (double.TryParse(EndEntry.Text, out double end)) _stage.End = end;

        _onSave?.Invoke(_stage);
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}