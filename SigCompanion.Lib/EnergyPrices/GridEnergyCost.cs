using System.Globalization;
using System.Text.Json;
using SigCompanion.Lib.Sigen.Models;

namespace SigCompanion.Lib.EnergyPrices;

public static class GridEnergyCost
{
    public static decimal? CalculateToday(SigenHistoricalComparisonSnapshot history, DateTime now,
        IReadOnlyList<(DateTime LocalTime, decimal BuyPrice, decimal SellPrice)> prices)
    {
        if (history.TodayDate != DateOnly.FromDateTime(now) || history.TodayChartData.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var chart = history.TodayChartData.Deserialize<SigenHistoricalChartData>();
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
                DateOnly.FromDateTime(time) != history.TodayDate || time > now)
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
