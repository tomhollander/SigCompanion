using Android.App;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using SigCompanion.Lib.Sigen;
using SigCompanion.Services;
using SigCompanion.ViewModels;
using System;
using System.Threading.Tasks;

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
            App.PlayBatteryAlertAsync = PlayBatteryAlertAsync;
            base.OnCreate(savedInstanceState);
            Window.SetFlags(WindowManagerFlags.Fullscreen, WindowManagerFlags.Fullscreen);
            // Prevent the screen from dimming or locking
            Window.AddFlags(WindowManagerFlags.KeepScreenOn);
            BackRequested += OnBackRequested;
        }

        private static async Task PlayBatteryAlertAsync(BatteryChargeAlert alert)
        {
            using var source = new BatterySoundDataSource(await BatterySoundAssets.ReadAsync(alert));
            using var player = new MediaPlayer();
            var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var playback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            player.Prepared += (_, _) => prepared.TrySetResult();
            player.Completion += (_, _) => playback.TrySetResult();
            player.Error += (_, e) =>
            {
                e.Handled = true;
                var error = new InvalidOperationException($"Battery sound playback failed: {e.What}.");
                if (!prepared.TrySetException(error))
                {
                    playback.TrySetException(error);
                }
            };
            try
            {
                player.SetDataSource(source);
                player.PrepareAsync();
                await prepared.Task;
                player.Start();
                await playback.Task;
            }
            finally
            {
                player.Release();
            }
        }

        private sealed class BatterySoundDataSource(byte[] wav) : MediaDataSource
        {
            public override long Size => wav.Length;

            public override int ReadAt(long position, byte[] buffer, int offset, int size)
            {
                if (position >= wav.Length)
                {
                    return -1;
                }

                var count = Math.Min(size, wav.Length - (int)position);
                Array.Copy(wav, (int)position, buffer, offset, count);
                return count;
            }

            public override void Close() { }
        }

        protected override void OnPause()
        {
            base.OnPause();
            App.MainViewModel?.OnApplicationDeactivated();
        }

        protected override void OnResume()
        {
            base.OnResume();
            App.MainViewModel?.OnApplicationActivated();
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
