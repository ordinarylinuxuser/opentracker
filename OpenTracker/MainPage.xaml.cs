#region

using OpenTracker.ViewModels;

#endregion

namespace OpenTracker;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        // Because Drawables inside GraphicsView don't inherit BindingContext automatically
        if (StatusGraphicsView.Drawable is TrackerDrawable drawable)
        {
            drawable.ViewModel = viewModel;
        }

        _viewModel.PropertyChanged += (s, e) =>
         {
             if (e.PropertyName == nameof(MainViewModel.TotalDurationValue) ||
                 e.PropertyName == nameof(MainViewModel.CurrentStage) ||
                 e.PropertyName == nameof(MainViewModel.IsTracking))
             {
                 StatusGraphicsView.Invalidate();
             }
         };
    }
}
public class TrackerDrawable : BindableObject, IDrawable
{
    public MainViewModel ViewModel { get; set; } = null!;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (ViewModel == null || ViewModel.CurrentConfig == null) return;

        canvas.Antialias = true;

        float centerX = dirtyRect.Center.X;
        float centerY = dirtyRect.Center.Y;
        float radius = Math.Min(dirtyRect.Width, dirtyRect.Height) / 2 - 25;

        double currentValue = ViewModel.TotalDurationValue;
        string type = ViewModel.CurrentConfig.DurationType?.ToLower() ?? "time";

        // 1. Determine Cycle Length based on DurationType
        double cycleLength = 24.0; // Default Time (24h)

        if (type == "day" || type == "days") cycleLength = 1.0; // 1 Day
        else if (type == "week" || type == "weeks") cycleLength = 1.0; // 1 Week

        // 2. Calculate Cycle Position
        int completedCycles = (int)(currentValue / cycleLength);
        double currentCycleProgress = currentValue % cycleLength;

        float sweepAngle = (float)(currentCycleProgress / cycleLength * 360);

        // 3. Determine Colors
        var activeColor = Color.FromArgb(ViewModel.CurrentStage?.ColorHex ?? "#2196F3");
        // If we have completed at least one cycle, dim the track color
        var trackColor = completedCycles > 0 ? activeColor.WithAlpha(0.3f) : Color.FromArgb("#333333");

        // 4. Draw Track
        canvas.StrokeColor = trackColor;
        canvas.StrokeSize = 10;
        canvas.DrawCircle(centerX, centerY, radius);

        // 5. Draw Progress Arc
        canvas.StrokeColor = activeColor;
        canvas.StrokeSize = 10;
        canvas.StrokeLineCap = LineCap.Round;
        // -90 is top, sweeping clockwise
        canvas.DrawArc(centerX - radius, centerY - radius, radius * 2, radius * 2, 90, 90 - sweepAngle, true, false);

        // 6. Draw Stage Emojis
        if (ViewModel.Stages != null)
        {
            canvas.FontSize = 20;

            double windowStart = completedCycles * cycleLength;
            double windowEnd = (completedCycles + 1) * cycleLength;

            foreach (var stage in ViewModel.Stages)
            {
                // Check overlap with current cycle window
                double overlapStart = Math.Max(stage.Start, windowStart);
                double overlapEnd = Math.Min(stage.End, windowEnd);

                if (overlapStart < overlapEnd)
                {
                    // Calculate relative start
                    double relativeStart = overlapStart - windowStart;

                    double angleRad = (relativeStart / cycleLength * 2 * Math.PI) - (Math.PI / 2);

                    float markerR = radius + 20;
                    float x = centerX + markerR * (float)Math.Cos(angleRad);
                    float y = centerY + markerR * (float)Math.Sin(angleRad);

                    if (stage.IsActive)
                    {
                        canvas.FillColor = activeColor.WithAlpha(0.2f);
                        canvas.FillCircle(x, y, 16);
                    }

                    canvas.FontColor = Colors.White;
                    canvas.DrawString(stage.Icon, x - 15, y - 15, 30, 30, HorizontalAlignment.Center, VerticalAlignment.Center);
                }
            }
        }

        // 7. Draw Badge for multiple cycles
        if (completedCycles > 0)
        {
            canvas.FontColor = activeColor;
            canvas.FontSize = 12;
            string cycleLabel = "Day";
            if (type.Contains("week")) cycleLabel = "Week";
            else if (type.Contains("day")) cycleLabel = "Day";

            // "Day 2", "Week 3", etc.
            canvas.DrawString($"{cycleLabel} {completedCycles + 1}", centerX - 25, centerY + 60, 50, 20, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}