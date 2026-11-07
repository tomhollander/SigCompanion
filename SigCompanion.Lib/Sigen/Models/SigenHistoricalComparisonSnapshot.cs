using System.Text.Json;

namespace SigCompanion.Lib.Sigen.Models;

public sealed class SigenHistoricalComparisonSnapshot
{
    public required DateOnly TodayDate { get; init; }
    public required DateOnly YesterdayDate { get; init; }
    public required IReadOnlyList<SigenStationCardComparisonRow> StationCards { get; init; }
    public required JsonElement TodayChartData { get; init; }
    public required JsonElement YesterdayChartData { get; init; }
}

public sealed class SigenStationCardComparisonRow
{
    public required string CardKey { get; init; }
    public decimal TodayKwh { get; init; }
    public decimal YesterdayKwh { get; init; }
}
