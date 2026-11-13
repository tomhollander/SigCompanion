using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using SigCompanion.ViewModels;

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
            BackRequested += OnBackRequested;
        }

        private void OnBackRequested(object? sender, AndroidBackRequestedEventArgs e)
        {
            if (App.MainViewModel is { IsSettingsPageVisible: true } viewModel)
            {
                viewModel.BackToMainCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
