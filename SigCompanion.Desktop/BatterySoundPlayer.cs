using SigCompanion.Lib.Sigen;
using SigCompanion.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SigCompanion.Desktop;

internal static class BatterySoundPlayer
{
    public static async Task PlayAsync(BatteryChargeAlert alert)
    {
        var wav = await BatterySoundAssets.ReadAsync(alert);
        if (OperatingSystem.IsWindows())
        {
            await Task.Run(() =>
            {
                if (!PlaySound(wav, IntPtr.Zero, SoundMemory | SoundNoDefault))
                {
                    throw new InvalidOperationException("Windows could not play the battery sound.");
                }
            });
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        try
        {
            await File.WriteAllBytesAsync(path, wav);
            var playerPath = OperatingSystem.IsMacOS()
                ? "/usr/bin/afplay"
                : File.Exists("/.flatpak-info") ? "/usr/bin/paplay" : "aplay";
            var startInfo = new ProcessStartInfo(playerPath)
            {
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(path);
            using var player = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the audio player.");
            await player.WaitForExitAsync();
            if (player.ExitCode != 0)
            {
                throw new InvalidOperationException($"Audio player exited with code {player.ExitCode}.");
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private const uint SoundNoDefault = 0x0002;
    private const uint SoundMemory = 0x0004;

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(byte[] sound, IntPtr module, uint flags);
}
