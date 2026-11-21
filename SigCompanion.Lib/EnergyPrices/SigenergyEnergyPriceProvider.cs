using System.Globalization;
using SigCompanion.Lib.Sigen;
using SigCompanion.Lib.Sigen.Models;

namespace SigCompanion.Lib.EnergyPrices;

public sealed class SigenergyEnergyPriceProvider(SigenUiApi api) : IEnergyPriceProvider
{
    private readonly SemaphoreSlim _stationLock = new(1, 1);
    private Station? _station;

    public CultureInfo CurrencyCulture => _station?.Culture ?? CultureInfo.InvariantCulture;
    public string MinorUnitSuffix => _station?.MinorUnitSuffix ?? string.Empty;

    public Task<EnergyPriceSnapshot> GetPricesAsync(CancellationToken cancellationToken = default) =>
        GetPricesAsync(null, DateTime.Now, cancellationToken);

    public async Task<EnergyPriceSnapshot> GetPricesAsync(SigenHistoricalComparisonSnapshot? history,
        DateTime now, CancellationToken cancellationToken = default)
    {
        var station = await GetStationAsync(cancellationToken).ConfigureAwait(false);
        var data = await api.GetHomePricePreviewAsync(station.Id, DateOnly.FromDateTime(now), cancellationToken)
            .ConfigureAwait(false);
        var prices = data.GetProperty("pricePoints").EnumerateArray()
            .Select(point => (LocalTime: point.GetProperty("localTime").GetDateTime(),
                BuyPrice: point.GetProperty("buyPrice").GetDecimal(),
                SellPrice: point.GetProperty("sellPrice").GetDecimal()))
            .ToArray();
        for (var i = prices.Length - 1; i >= 0; i--)
        {
            var point = prices[i];
            if (point.LocalTime >= now)
            {
                continue;
            }

            return new EnergyPriceSnapshot(
                new EnergyTariff(point.BuyPrice * 100m, "General usage", string.Empty, 0),
                new EnergyTariff(point.SellPrice * 100m, "Feed-in", string.Empty, 0),
                history is null ? null : GridEnergyCost.CalculateToday(history, now, prices));
        }

        throw new InvalidOperationException("Sigenergy returned no price point before the current local time.");
    }

    private async Task<Station> GetStationAsync(CancellationToken cancellationToken)
    {
        await _stationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_station is not null)
            {
                return _station;
            }

            var data = await api.GetHomeStationAsync(cancellationToken).ConfigureAwait(false);
            var id = data.GetProperty("stationId").GetInt64();
            var countryCode = data.GetProperty("countryCode").GetString();
            var culture = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                .Where(culture => string.Equals(new RegionInfo(culture.Name).TwoLetterISORegionName,
                    countryCode, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(culture => culture.TwoLetterISOLanguageName == "en")
                .ThenBy(culture => culture.Name, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("Sigenergy returned an unsupported station country code.");
            var suffix = new RegionInfo(culture.Name).ISOCurrencySymbol switch
            {
                "AUD" or "CAD" or "NZD" or "USD" or "EUR" => "c",
                "GBP" => "p",
                _ => string.Empty,
            };
            return _station = new Station(id, culture, suffix);
        }
        finally
        {
            _stationLock.Release();
        }
    }

    private sealed record Station(long Id, CultureInfo Culture, string MinorUnitSuffix);
}
