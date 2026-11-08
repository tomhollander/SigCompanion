using System;

namespace SigCompanion.Lib.Sigen.Models;

public sealed class PowerFlowAllocation
{
    public required double SolarPower { get; init; }
    public required double HomePower { get; init; }
    public required double GridPower { get; init; }
    public required double BatteryPower { get; init; }

    public double GridToHome =>
        GridPower <= 0 ? 0 : Math.Min(GridPower, HomePower);

    public double GridToBattery
    {
        get
        {
            if (GridPower <= 0 || BatteryPower >= 0) return 0;

            var gridSurplus = Math.Max(0, GridPower - GridToHome);
            return Math.Min(gridSurplus, Math.Abs(BatteryPower));
        }
    }

    public double SolarToHome
    {
        get
        {
            var remainingHomePower = Math.Max(0, HomePower - GridToHome - BatteryToHome);
            return Math.Min(SolarPower, remainingHomePower);
        }
    }

    public double SolarToBattery
    {
        get
        {
            if (BatteryPower >= 0) return 0;

            var remainingBatteryCharge = Math.Max(0, Math.Abs(BatteryPower) - GridToBattery);
            var solarSurplus = Math.Max(0, SolarPower - SolarToHome);
            return Math.Min(solarSurplus, remainingBatteryCharge);
        }
    }

    public double SolarToGrid =>
        Math.Max(0, SolarPower - SolarToHome - SolarToBattery);

    public double BatteryToHome
    {
        get
        {
            if (BatteryPower <= 0) return 0;

            var remainingHomePower = Math.Max(0, HomePower - GridToHome);
            return Math.Min(BatteryPower, remainingHomePower);
        }
    }

    public double BatteryToGrid =>
        BatteryPower > 0 ? Math.Max(0, BatteryPower - BatteryToHome) : 0;
}
