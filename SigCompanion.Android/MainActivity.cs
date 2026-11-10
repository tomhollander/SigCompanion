using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;

namespace SigCompanion.Android
{
    [Activity(
        Label = "Sig Companion",
        Theme = "@style/MyTheme.NoActionBar",
        Icon = "@mipmap/ic_launcher",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AvaloniaMainActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Window.SetFlags(WindowManagerFlags.Fullscreen, WindowManagerFlags.Fullscreen);
            // Prevent the screen from dimming or locking
            Window.AddFlags(WindowManagerFlags.KeepScreenOn);
        }
    }
}
