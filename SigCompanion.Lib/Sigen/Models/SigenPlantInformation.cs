namespace SigCompanion.Lib.Sigen.Models;

public sealed class SigenPlantInformation
{
    public uint SystemTimeEpochSeconds { get; init; }
    public ushort SystemTimeZoneMinutes { get; init; }
    public ushort EmsWorkMode { get; init; }
    public ushort GridSensorStatus { get; init; }
    public decimal GridSensorActivePower { get; init; }
    public decimal GridSensorReactivePower { get; init; }
    public ushort OnOffGridStatus { get; init; }
    public decimal MaxActivePower { get; init; }
    public decimal MaxApparentPower { get; init; }
    public decimal EnergyStorageSystemSoc { get; init; }
    public decimal PlantPhaseAActivePower { get; init; }
    public decimal PlantPhaseBActivePower { get; init; }
    public decimal PlantPhaseCActivePower { get; init; }
    public decimal PlantPhaseAReactivePower { get; init; }
    public decimal PlantPhaseBReactivePower { get; init; }
    public decimal PlantPhaseCReactivePower { get; init; }
    public ushort GeneralAlarm1 { get; init; }
    public ushort GeneralAlarm2 { get; init; }
    public ushort GeneralAlarm3 { get; init; }
    public ushort GeneralAlarm4 { get; init; }
    public decimal PlantActivePower { get; init; }
    public decimal PlantReactivePower { get; init; }
    public decimal PhotovoltaicPower { get; init; }
    public decimal EssPower { get; init; }
    public decimal AvailableMaxActivePower { get; init; }
    public decimal AvailableMinActivePower { get; init; }
    public decimal AvailableMaxReactivePower { get; init; }
    public decimal AvailableMinReactivePower { get; init; }
    public decimal AvailableMaxChargingPower { get; init; }
    public decimal AvailableMaxDischargingPower { get; init; }
    public ushort PlantRunningState { get; init; }
    public decimal GridSensorPhaseAActivePower { get; init; }
    public decimal GridSensorPhaseBActivePower { get; init; }
    public decimal GridSensorPhaseCActivePower { get; init; }
    public decimal GridSensorPhaseAReactivePower { get; init; }
    public decimal GridSensorPhaseBReactivePower { get; init; }
    public decimal GridSensorPhaseCReactivePower { get; init; }
    public decimal AvailableMaxChargingCapacity { get; init; }
    public decimal AvailableMaxDischargingCapacity { get; init; }
    public decimal RatedEssChargingPower { get; init; }
    public decimal RatedEssDischargingPower { get; init; }
}
