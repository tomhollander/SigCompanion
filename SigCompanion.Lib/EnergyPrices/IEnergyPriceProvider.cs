using System.Globalization;

namespace SigCompanion.Lib.EnergyPrices;

public interface IEnergyPriceProvider
{
    CultureInfo CurrencyCulture { get; }
    string MinorUnitSuffix { get; }

    Task<EnergyPriceSnapshot> GetPricesAsync(CancellationToken cancellationToken = default);
}

public sealed record EnergyTariff(decimal CentsPerKwh, string TariffName, string PriceLevelName, int PriceLevel);

public sealed record EnergyPriceSnapshot(EnergyTariff Buy, EnergyTariff Sell, decimal? TodayTotalDollars,
    decimal? YesterdayTotalDollars = null);
