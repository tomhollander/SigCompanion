using Avalonia.Media;

namespace SigCompanion.Models;

public static class PowerColors
{
    public const string HomeHex = "#A67BE5";
    public const string GridHex = "#307EDD";
    public const string SolarHex = "#CECB65";
    public const string BatteryHex = "#54ABAD";
    public const string EvHex = "#E03C3C";

    public static readonly Color Home = Color.Parse(HomeHex);
    public static readonly Color Grid = Color.Parse(GridHex);
    public static readonly Color Solar = Color.Parse(SolarHex);
    public static readonly Color Battery = Color.Parse(BatteryHex);
    public static readonly Color Ev = Color.Parse(EvHex);

    public static readonly IBrush HomeBrush = new SolidColorBrush(Home);
    public static readonly IBrush GridBrush = new SolidColorBrush(Grid);
    public static readonly IBrush SolarBrush = new SolidColorBrush(Solar);
    public static readonly IBrush BatteryBrush = new SolidColorBrush(Battery);
    public static readonly IBrush EvBrush = new SolidColorBrush(Ev);
}
