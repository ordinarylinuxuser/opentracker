#region

using _Microsoft.Android.Resource.Designer;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using OpenTracker.Utilities;
using System.Timers;
using Timer = System.Timers.Timer;

#endregion

namespace OpenTracker;

// The Attribute below automatically adds the <service> tag to the AndroidManifest.xml

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
public class TickerService : Service
{
    private Timer _timer;
    private DateTime _startTime;
    private string _trackerTitle;
    private string _currentStage;
    private string _displayFormat;

    private const string ChannelId = "open_tracker_channel";
    private const int NotificationId = 1001;

    public override IBinder OnBind(Intent intent)
    {
        return null;
    }

    public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == "STOP_SERVICE")
        {
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        if (intent != null)
        {
            if (intent.HasExtra("UpdateStageOnly"))
            {
                _currentStage = intent.GetStringExtra("StageName");
                UpdateNotification();
                return StartCommandResult.Sticky;
            }
            else
            {
                var ticks = intent.GetLongExtra("StartTime", DateTime.Now.Ticks);
                _startTime = new DateTime(ticks);
                _trackerTitle = intent.GetStringExtra("TrackerName") ?? "OpenTracker";
                _currentStage = intent.GetStringExtra("StageName") ?? "Tracking...";
                _displayFormat = intent.GetStringExtra("DisplayFormat") ?? "Time";
            }
        }

        CreateNotificationChannel();

        // Build the initial notification
        var notification = BuildNotification();

        // Start Foreground Service
        // For Android 14+ (API 34), we explicitly state the type if compiling against latest SDK
        if (Build.VERSION.SdkInt >= BuildVersionCodes.UpsideDownCake)
            StartForeground(NotificationId, notification,
                ForegroundService.TypeDataSync);
        else
            StartForeground(NotificationId, notification);

        if (_timer == null)
        {
            // We still keep the timer to check for Stage transitions,
            // but the UI tick is handled by the Chronometer for "Time" mode.
            _timer = new Timer(1000);
            _timer.Elapsed += OnTimerTick;
            _timer.Start();
        }

        return StartCommandResult.Sticky;
    }

    private void OnTimerTick(object sender, ElapsedEventArgs e)
    {
        // We periodically re-issue the notification.
        // This ensures Stage transitions are reflected.
        // For "Time" format, the Chronometer handles the seconds visually.
        UpdateNotification();
    }

    private void UpdateNotification()
    {
        var notification = BuildNotification();
        var manager = GetSystemService(NotificationService) as NotificationManager;
        manager?.Notify(NotificationId, notification);
    }

    private Notification BuildNotification()
    {
        var elapsed = DateTime.Now - _startTime;

        // Intent to open app when tapped
        var intent = PackageManager?.GetLaunchIntentForPackage(PackageName);
        var pendingIntent = PendingIntent.GetActivity(this, 0, intent, PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle(_trackerTitle)
            .SetSmallIcon(ResourceConstant.Drawable.ic_stat_tracker) // Ensure this icon exists
            .SetContentIntent(pendingIntent)
            .SetOnlyAlertOnce(true)
            .SetOngoing(true);

        // FIX: Use Native Android Chronometer for standard "Time" format.
        // This eliminates the visual lag caused by delayed timer ticks.
        if (_displayFormat == "Time")
        {
            // Convert .NET DateTime to Java Epoch Milliseconds
            var javaStartTime = new DateTimeOffset(_startTime).ToUnixTimeMilliseconds();

            builder.SetContentText($"{_currentStage}"); // Just stage name, time is handled by system
            builder.SetWhen(javaStartTime);
            builder.SetUsesChronometer(true);
        }
        else
        {
            // For custom formats (Weeks, Days, etc), keep the manual update
            // as Chronometer only supports H:MM:SS
            var timeString = FormatHelper.FormatTime(elapsed, _displayFormat);
            builder.SetContentText($"{_currentStage}  {timeString}");
            builder.SetUsesChronometer(false);
            builder.SetWhen(0); // Hide timestamp
        }

        return builder.Build();
    }

    public override void OnDestroy()
    {
        _timer?.Stop();
        _timer?.Dispose();
        base.OnDestroy();
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        var manager = GetSystemService(NotificationService) as NotificationManager;
        if (manager == null) return;

        var channel = new NotificationChannel(ChannelId, "Open Tracker Progress", NotificationImportance.Default)
        {
            Description = "Shows ongoing tracking progress"
        };
        manager.CreateNotificationChannel(channel);
    }
}