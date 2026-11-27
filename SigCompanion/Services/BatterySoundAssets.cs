using Avalonia.Platform;
using SigCompanion.Lib.Sigen;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SigCompanion.Services;

public static class BatterySoundAssets
{
    public static async Task<byte[]> ReadAsync(BatteryChargeAlert alert)
    {
        var name = alert == BatteryChargeAlert.Empty ? "battery-empty.wav" : "battery-full.wav";
        using var asset = AssetLoader.Open(new Uri($"avares://SigCompanion/Assets/{name}"));
        using var buffer = new MemoryStream();
        await asset.CopyToAsync(buffer);
        return buffer.ToArray();
    }
}
