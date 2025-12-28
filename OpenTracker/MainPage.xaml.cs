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
            if (e.PropertyName == nameof(MainViewModel.Progress) ||
                e.PropertyName == nameof(MainViewModel.CurrentStage))
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
        if (ViewModel == null) return;

        canvas.Antialias = true;

        float centerX = dirtyRect.Center.X;
        float centerY = dirtyRect.Center.Y;
        float radius = Math.Min(dirtyRect.Width, dirtyRect.Height) / 2 - 25; // More padding for emojis

        double totalHours = ViewModel.TotalHours;

        // 1. Calculate 24h Cycle Info
        int currentDay = (int)(totalHours / 24); // 0 = Day 1, 1 = Day 2...
        double currentDayHours = totalHours % 24; // 0 to 24

        // Map 0..24h to 0..360 degrees
        // -90 degrees is 12:00 PM (Top of circle)
        //float startAngle = 90;
        float sweepAngle = (float)(currentDayHours / 24.0 * 360);

        // 2. Determine Colors
        // If we are past 24 hours, the "track" isn't empty gray anymore, 
        // it's the color of the previous completed day (e.g., a dimmer version of primary).
        var activeColor = Color.FromArgb(ViewModel.CurrentStage?.ColorHex ?? "#2196F3");
        var trackColor = currentDay > 0 ? activeColor.WithAlpha(0.3f) : Color.FromArgb("#333333");

        // 3. Draw Track (Background Circle)
        canvas.StrokeColor = trackColor;
        canvas.StrokeSize = 10;
        canvas.DrawCircle(centerX, centerY, radius);

        // 4. Draw Progress Arc (Current Day)
        canvas.StrokeColor = activeColor;
        canvas.StrokeSize = 10;
        canvas.StrokeLineCap = LineCap.Round;

        // DrawArc(x, y, w, h, startAngle, endAngle, clockwise, closed)
        // Note: 90 is top, sweeping clockwise.
        canvas.DrawArc(centerX - radius, centerY - radius, radius * 2, radius * 2, 90, 90 - sweepAngle, true, false);

        // 5. Draw Stage Emojis (Context Aware)
        if (ViewModel.Stages != null)
        {
            canvas.FontSize = 20; // Bigger emojis

            // Define the time window for the current circle (e.g., 24h to 48h)
            double windowStart = currentDay * 24;
            double windowEnd = (currentDay + 1) * 24;

            foreach (var stage in ViewModel.Stages)
            {
                // Does this stage exist in the current 24h window?
                // It exists if it overlaps with [windowStart, windowEnd]
                // Intersection logic: Max(startA, startB) < Min(endA, endB)
                double overlapStart = Math.Max(stage.StartHour, windowStart);
                double overlapEnd = Math.Min(stage.EndHour, windowEnd);

                if (overlapStart < overlapEnd)
                {
                    // Calculate where this stage starts *relative* to the current clock face (0-24)
                    double relativeStartHour = overlapStart - windowStart;

                    // Convert hour (0-24) to Angle (-PI/2 to 3PI/2)
                    // 0h = -90 deg (Top)
                    double angleRad = (relativeStartHour / 24.0 * 2 * Math.PI) - (Math.PI / 2);

                    // Position the emoji on the ring
                    float markerR = radius + 20; // Push out past the stroke
                    float x = centerX + markerR * (float)Math.Cos(angleRad);
                    float y = centerY + markerR * (float)Math.Sin(angleRad);

                    // Highlight if active
                    if (stage.IsActive)
                    {
                        // Optional: Draw a glow behind active emoji
                        canvas.FillColor = activeColor.WithAlpha(0.2f);
                        canvas.FillCircle(x, y, 16);
                    }

                    canvas.FontColor = Colors.White;
                    // Draw centered emoji
                    canvas.DrawString(stage.Icon, x - 15, y - 15, 30, 30, HorizontalAlignment.Center, VerticalAlignment.Center);
                }
            }
        }

        // 6. Draw "Day X" Badge if > 24h
        if (currentDay > 0)
        {
            canvas.FontColor = activeColor;
            canvas.FontSize = 12;
            canvas.DrawString($"Day {currentDay + 1}", centerX - 25, centerY + 60, 50, 20, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}