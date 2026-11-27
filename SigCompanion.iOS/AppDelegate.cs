using Avalonia;
using Avalonia.Controls;
using Avalonia.iOS;
using Avalonia.Media;
using AVFoundation;
using Foundation;
using SigCompanion.Lib.Sigen;
using SigCompanion.Services;
using System;
using System.Threading.Tasks;
using UIKit;

namespace SigCompanion.iOS
{
    // The UIApplicationDelegate for the application. This class is responsible for launching the 
    // User Interface of the application, as well as listening (and optionally responding) to 
    // application events from iOS.
    [Register("AppDelegate")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            App.PlayBatteryAlertAsync = PlayBatteryAlertAsync;
            return base.CustomizeAppBuilder(builder)
                .WithInterFont();
        }

        private static async Task PlayBatteryAlertAsync(BatteryChargeAlert alert)
        {
            using var data = NSData.FromArray(await BatterySoundAssets.ReadAsync(alert));
            using var player = AVAudioPlayer.FromData(data, out var error)
                ?? throw new InvalidOperationException(error?.LocalizedDescription ?? "Could not load the battery sound.");
            var playback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            player.FinishedPlaying += (_, e) =>
            {
                if (e.Status)
                {
                    playback.TrySetResult();
                }
                else
                {
                    playback.TrySetException(new InvalidOperationException("Could not finish playing the battery sound."));
                }
            };
            player.DecoderError += (_, e) => playback.TrySetException(new InvalidOperationException(e.Error.LocalizedDescription));
            if (!player.Play())
            {
                throw new InvalidOperationException("Could not play the battery sound.");
            }

            await playback.Task;
        }
    }
}
