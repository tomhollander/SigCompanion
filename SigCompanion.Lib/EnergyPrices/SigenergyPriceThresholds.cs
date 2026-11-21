namespace SigCompanion.Lib.EnergyPrices;

public sealed record SigenergyPriceThresholds(int LowBelow, int VeryHighAt)
{
    public static SigenergyPriceThresholds DefaultBuy { get; } = new(20, 40);
    public static SigenergyPriceThresholds DefaultSell { get; } = new(6, 20);

    public EnergyTariff Label(EnergyTariff tariff)
    {
        var midpoint = (LowBelow + (decimal)VeryHighAt) / 2m;
        var (name, level) = tariff.CentsPerKwh switch
        {
            < 0m => ("Ext Low", 1),
            var price when price < LowBelow / 2m => ("Very Low", 2),
            var price when price < LowBelow => ("Low", 3),
            var price when price < midpoint => ("Medium", 4),
            var price when price < VeryHighAt => ("High", 5),
            var price when price < VeryHighAt + midpoint => ("Very high", 6),
            _ => ("Ext High", 7),
        };

        return tariff with { PriceLevelName = name, PriceLevel = level };
    }
}
