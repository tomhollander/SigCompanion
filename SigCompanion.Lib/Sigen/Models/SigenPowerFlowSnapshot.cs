namespace SigCompanion.Lib.Sigen.Models;

public sealed class SigenPowerFlowSnapshot
{
    public decimal PvPower { get; init; }
    public decimal GridPower { get; init; }
    public decimal EvPower { get; init; }
    public decimal AcPower { get; init; }
    public decimal LoadPower { get; init; }
    public decimal HeatPumpPower { get; init; }
    public decimal BatteryPower { get; init; }
}
