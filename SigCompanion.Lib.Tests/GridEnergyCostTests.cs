using System.Text.Json;
using SigCompanion.Lib.EnergyPrices;
using SigCompanion.Lib.Sigen.Models;

namespace SigCompanion.Lib.Tests;

public sealed class GridEnergyCostTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 0, 16, 0);

    [Fact]
    public void PricesEachFiveMinuteGridSampleAtItsEffectiveRate()
    {
        var total = GridEnergyCost.CalculateToday(CreateHistory(), Now,
            [(Now.Date, 0.40m, 0.10m), (Now.Date.AddMinutes(10), 0.20m, 0.05m)]);

        Assert.Equal(0.40m, total);
    }

    [Fact]
    public void ReturnsNoTotalWhenNonzeroSampleHasNoApplicableRate()
    {
        var total = GridEnergyCost.CalculateToday(CreateHistory(), Now,
            [(Now.Date.AddMinutes(5), 0.40m, 0.10m)]);

        Assert.Null(total);
    }

    private static SigenHistoricalComparisonSnapshot CreateHistory()
    {
        using var document = JsonDocument.Parse("""
            {"dataSeries":[
                {"id":"GRID","points":[
                    {"time":"20261121 00:00","value":"12"},
                    {"time":"20261121 00:05","value":-12},
                    {"time":"20261121 00:10","value":6},
                    {"time":"20261121 00:20","value":120}
                ]},
                {"id":"TO_LOAD","points":[{"time":"20261121 00:00","value":-100}]}
            ]}
            """);
        return new SigenHistoricalComparisonSnapshot
        {
            TodayDate = DateOnly.FromDateTime(Now),
            YesterdayDate = DateOnly.FromDateTime(Now.AddDays(-1)),
            StationCards = [],
            TodayChartData = document.RootElement.Clone(),
            YesterdayChartData = default,
        };
    }
}
