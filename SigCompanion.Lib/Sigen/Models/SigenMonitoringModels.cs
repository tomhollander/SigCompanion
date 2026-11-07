using System.Text.Json;
using System.Text.Json.Serialization;

namespace SigCompanion.Lib.Sigen.Models;

public sealed class SigenPagedResult<T>
{
    [JsonPropertyName("records")]
    public List<T>? Records { get; init; }

    [JsonPropertyName("items")]
    public List<T>? Items { get; init; }

    [JsonPropertyName("total")]
    public int? Total { get; init; }

    [JsonPropertyName("pageNo")]
    public int? PageNo { get; init; }

    [JsonPropertyName("pageSize")]
    public int? PageSize { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

public sealed class SigenSystemInfo
{
    [JsonPropertyName("systemId")]
    public string? SystemId { get; init; }

    [JsonPropertyName("systemName")]
    public string? SystemName { get; init; }

    [JsonPropertyName("addr")]
    public string? Address { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("isActivate")]
    public bool? IsActivate { get; init; }

    [JsonPropertyName("onOffGridStatus")]
    public string? OnOffGridStatus { get; init; }

    [JsonPropertyName("timeZone")]
    public string? TimeZone { get; init; }

    [JsonPropertyName("gridConnectedTime")]
    public long? GridConnectedTime { get; init; }

    [JsonPropertyName("pvCapacity")]
    public decimal? PvCapacity { get; init; }

    [JsonPropertyName("batteryCapacity")]
    public decimal? BatteryCapacity { get; init; }
}

public sealed class SigenDeviceInfo
{
    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; init; }

    [JsonPropertyName("sn")]
    public string? Sn { get; init; }

    [JsonPropertyName("deviceType")]
    public string? DeviceType { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

public sealed class SigenSystemRealtimeData
{
    [JsonPropertyName("systemId")]
    public string? SystemId { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }

    [JsonPropertyName("pvPower")]
    public decimal? PvPower { get; init; }

    [JsonPropertyName("loadPower")]
    public decimal? LoadPower { get; init; }

    [JsonPropertyName("gridPower")]
    public decimal? GridPower { get; init; }

    [JsonPropertyName("batteryPower")]
    public decimal? BatteryPower { get; init; }

    [JsonPropertyName("soc")]
    public decimal? Soc { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

public sealed class SigenEnergyFlowData
{
    [JsonPropertyName("systemId")]
    public string? SystemId { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }

    [JsonPropertyName("pvToLoad")]
    public decimal? PvToLoad { get; init; }

    [JsonPropertyName("pvToBattery")]
    public decimal? PvToBattery { get; init; }

    [JsonPropertyName("gridToLoad")]
    public decimal? GridToLoad { get; init; }

    [JsonPropertyName("batteryToLoad")]
    public decimal? BatteryToLoad { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

public sealed class SigenDeviceRealtimeData
{
    [JsonPropertyName("systemId")]
    public string? SystemId { get; init; }

    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; init; }

    [JsonPropertyName("deviceType")]
    public string? DeviceType { get; init; }

    [JsonPropertyName("realTimeInfo")]
    public SigenDeviceRealtimeInfo? RealTimeInfo { get; init; }
}

public sealed class SigenDeviceRealtimeInfo
{
    [JsonPropertyName("batSoc")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? BatSoc { get; init; }

    [JsonPropertyName("batPower")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? BatPower { get; init; }
}

public sealed class SigenHistoricalSeries
{
    [JsonPropertyName("systemId")]
    public string? SystemId { get; init; }

    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; init; }

    [JsonPropertyName("points")]
    public List<SigenHistoricalDataPoint>? Points { get; init; }

    [JsonPropertyName("list")]
    public List<SigenHistoricalDataPoint>? List { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

public sealed class SigenHistoricalDataPoint
{
    [JsonPropertyName("time")]
    public DateTimeOffset? Time { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }

    [JsonPropertyName("value")]
    public decimal? Value { get; init; }

    [JsonPropertyName("unit")]
    public string? Unit { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}
