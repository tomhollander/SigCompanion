using System.Text.Json.Serialization;

namespace SigCompanion.Lib.Sigen.Models;

public sealed class SigenHistoryDayData
{
    [JsonPropertyName("itemList")]
    public List<SigenHistoryDayItem>? ItemList { get; init; }
}

public sealed class SigenHistoryDayItem
{
    [JsonPropertyName("dataTime")]
    public string? DataTime { get; init; }

    [JsonPropertyName("batSoc")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? BatSoc { get; init; }
}

public sealed class SigenStateOfChargePoint
{
    public required DateTime Time { get; init; }
    public required decimal StateOfCharge { get; init; }
}
