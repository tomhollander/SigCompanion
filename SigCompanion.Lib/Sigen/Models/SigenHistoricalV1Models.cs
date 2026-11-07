using System.Text.Json;
using System.Text.Json.Serialization;

namespace SigCompanion.Lib.Sigen.Models;

public sealed class SigenHistoricalV1Data
{
    [JsonPropertyName("sankeyData")]
    public SigenSankeyData? SankeyData { get; init; }

    [JsonPropertyName("chartData")]
    public SigenHistoricalChartData? ChartData { get; init; }

    [JsonPropertyName("stationCards")]
    public List<SigenStationCard>? StationCards { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

public sealed class SigenSankeyData
{
    [JsonPropertyName("nodes")]
    public List<SigenSankeyNode>? Nodes { get; init; }

    [JsonPropertyName("links")]
    public List<SigenSankeyLink>? Links { get; init; }
}

public sealed class SigenSankeyNode
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("value")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Value { get; init; }
}

public sealed class SigenSankeyLink
{
    [JsonPropertyName("sourceId")]
    public string? SourceId { get; init; }

    [JsonPropertyName("targetId")]
    public string? TargetId { get; init; }

    [JsonPropertyName("value")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Value { get; init; }
}

public sealed class SigenHistoricalChartData
{
    [JsonPropertyName("chartName")]
    public string? ChartName { get; init; }

    [JsonPropertyName("unit")]
    public string? Unit { get; init; }

    [JsonPropertyName("chartType")]
    public string? ChartType { get; init; }

    [JsonPropertyName("dataSeries")]
    public List<SigenHistoricalSeriesData>? DataSeries { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

public sealed class SigenHistoricalSeriesData
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("points")]
    public List<SigenHistoricalChartPoint>? Points { get; init; }
}

public sealed class SigenHistoricalChartPoint
{
    [JsonPropertyName("time")]
    public string? Time { get; init; }

    [JsonPropertyName("value")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Value { get; init; }
}

public sealed class SigenStationCard
{
    [JsonPropertyName("cardKey")]
    public string? CardKey { get; init; }

    [JsonPropertyName("value")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Value { get; init; }
}
