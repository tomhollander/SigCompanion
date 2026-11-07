namespace SigCompanion.Lib.Sigen.Models;

public enum BatteryStatus
{
    Idle = 0,
    Charging = 1,
    Discharging = 2,
}

public sealed class SigenBatteryStatusSnapshot
{
    public decimal BatteryStateOfCharge { get; init; }
    public BatteryStatus BatteryStatus { get; init; }
    public bool IsGridConnected { get; init; }
}
