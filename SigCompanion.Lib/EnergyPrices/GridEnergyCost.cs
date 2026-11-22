using System.Globalization;
using System.Text.Json;
using SigCompanion.Lib.Sigen.Models;

namespace SigCompanion.Lib.EnergyPrices;

public static class GridEnergyCost
{
    public static decimal? CalculateToday(SigenHistoricalComparisonSnapshot history, DateTime now,
        IReadOnlyList<(DateTime LocalTime, decimal BuyPrice, decimal SellPrice)> prices) =>
        history.TodayDate == DateOnly.FromDateTime(now)
            ? Calculate(history.TodayChartData, history.TodayDate, now, prices)
            : null;

    public static decimal? CalculateYesterday(SigenHistoricalComparisonSnapshot history, DateTime now,
        IReadOnlyList<(DateTime LocalTime, decimal BuyPrice, decimal SellPrice)> prices) =>
        history.YesterdayDate == DateOnly.FromDateTime(now).AddDays(-1)
            ? Calculate(history.YesterdayChartData, history.YesterdayDate, now, prices)
            : null;

    private static decimal? Calculate(JsonElement chartData, DateOnly date, DateTime now,
        IReadOnlyList<(DateTime LocalTime, decimal BuyPrice, decimal SellPrice)> prices)
    {
        if (chartData.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var chart = chartData.Deserialize<SigenHistoricalChartData>();
        var samples = (chart?.DataSeries ?? [])
            .Where(series => series.Id == "GRID")
            .SelectMany(series => series.Points ?? []);
        var orderedPrices = prices.OrderBy(price => price.LocalTime).ToArray();
        decimal cost = 0m;
        var hasSamples = false;

        foreach (var sample in samples)
        {
            if (sample.Value is not decimal power ||
                !DateTime.TryParseExact(sample.Time, "yyyyMMdd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ||
                DateOnly.FromDateTime(time) != date || time > now)
            {
                continue;
            }

            hasSamples = true;
            if (power == 0m)
            {
                continue;
            }

            var index = Array.FindLastIndex(orderedPrices, price => price.LocalTime <= time);
            if (index < 0)
            {
                return null;
            }

            var price = orderedPrices[index];
            cost += power * (power > 0m ? price.BuyPrice : price.SellPrice) * 5m / 60m;
        }

        return hasSamples ? cost : null;
    }
}
