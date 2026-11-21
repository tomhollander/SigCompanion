using System;
using SigCompanion.Lib.EnergyPrices;

namespace SigCompanion.Models;

public sealed class AppSettings
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool SavePassword { get; set; }
    public string? Region { get; set; }
    public string? AccessToken { get; set; }
    public DateTimeOffset? AccessTokenExpiresAt { get; set; }
    public string? SystemId { get; set; }
    public bool ShowClock { get; set; }
    public bool CyclePagesWhenIdle { get; set; }
    public bool EnableAnimations { get; set; } = true;
    public bool ShowEnergyPrices { get; set; }
    public string EnergyPriceProvider { get; set; } = EnergyPriceProviderFactory.Amber;
    public string? AmberApiKey { get; set; }
    public SigenergyPriceThresholds SigenergyBuyThresholds { get; set; } = SigenergyPriceThresholds.DefaultBuy;
    public SigenergyPriceThresholds SigenergySellThresholds { get; set; } = SigenergyPriceThresholds.DefaultSell;
    public string ChartData { get; set; } = "Power";
    public bool UseModbus { get; set; }
    public string? ModbusHost { get; set; }
    public int ModbusPort { get; set; } = 502;
}
