using Avalonia;
using Avalonia.Browser;
using SigCompanion;
using SigCompanion.Services;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

internal sealed partial class Program
{
    private static Task Main(string[] args) => BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");

    public static AppBuilder BuildAvaloniaApp()
    {
        App.PlayBatteryAlertAsync = async alert => await PlayBatterySoundAsync(await BatterySoundAssets.ReadAsync(alert));
        return AppBuilder.Configure<App>();
    }

    [JSImport("playBatterySound", "batteryAudio")]
    internal static partial Task PlayBatterySoundAsync(byte[] wav);
}