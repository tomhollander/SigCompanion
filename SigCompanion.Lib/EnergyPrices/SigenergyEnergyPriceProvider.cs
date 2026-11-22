using System.Globalization;
using System.Text.Json;
using SigCompanion.Lib.Sigen;

namespace SigCompanion.Lib.EnergyPrices;

public sealed class SigenergyEnergyPriceProvider(SigenUiApi api) : IEnergyPriceProvider
{
    private readonly SemaphoreSlim _stationLock = new(1, 1);
    private Station? _station;

    public CultureInfo CurrencyCulture => _station?.Culture ?? CultureInfo.InvariantCulture;
    public string MinorUnitSuffix => _station?.MinorUnitSuffix ?? string.Empty;

    public Task<EnergyPriceSnapshot> GetPricesAsync(CancellationToken cancellationToken = default) =>
        GetPricesAsync(DateTime.Now, cancellationToken);

    public async Task<EnergyPriceSnapshot> GetPricesAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var station = await GetStationAsync(cancellationToken).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(now);
        var data = await api.GetHomePricePreviewAsync(station.Id, today, cancellationToken)
            .ConfigureAwait(false);
        var prices = ReadPrices(data);
        for (var i = prices.Length - 1; i >= 0; i--)
        {
            var point = prices[i];
            if (point.LocalTime >= now)
            {
                continue;
            }

            var todayTotal = await TryGetBillCostAsync(station.Id, today, historical: false, cancellationToken)
                .ConfigureAwait(false);
            var yesterdayTotal = await TryGetBillCostAsync(station.Id, today.AddDays(-1), historical: true, cancellationToken)
                .ConfigureAwait(false);

            return new EnergyPriceSnapshot(
                new EnergyTariff(point.BuyPrice * 100m, "General usage", string.Empty, 0),
                new EnergyTariff(point.SellPrice * 100m, "Feed-in", string.Empty, 0),
                todayTotal, yesterdayTotal);
        }

        throw new InvalidOperationException("Sigenergy returned no price point before the current local time.");
    }

    private async Task<decimal?> TryGetBillCostAsync(long stationId, DateOnly date, bool historical,
        CancellationToken cancellationToken)
    {
        try
        {
            var total = await (historical
                ? api.GetHistoricalDailyBillTotalAsync(stationId, date, cancellationToken)
                : api.GetDailyBillTotalAsync(stationId, date, cancellationToken)).ConfigureAwait(false);
            return -total;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A missing bill should not hide the current tariffs.
            return null;
        }
    }

    private static (DateTime LocalTime, decimal BuyPrice, decimal SellPrice)[] ReadPrices(JsonElement data) =>
        data.GetProperty("pricePoints").EnumerateArray()
            .Select(point => (point.GetProperty("localTime").GetDateTime(),
                point.GetProperty("buyPrice").GetDecimal(), point.GetProperty("sellPrice").GetDecimal()))
            .ToArray();

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
