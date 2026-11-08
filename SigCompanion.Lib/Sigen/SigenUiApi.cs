using System.Text.Json;
using SigCompanion.Lib.Sigen.Models;

namespace SigCompanion.Lib.Sigen;

public sealed class SigenUiApi
{
    private const string HistoricalLevelDay = "Day";
    private static readonly TimeSpan DefaultCachePeriod = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan HistoricalYesterdayCachePeriod = TimeSpan.FromDays(400);

    private readonly SigenCachedApiClient _cachedApiClient;

    public SigenUiApi(SigenCachedApiClient cachedApiClient)
    {
        _cachedApiClient = cachedApiClient ?? throw new ArgumentNullException(nameof(cachedApiClient));
    }

    public void SetLoginCredentials(string username, string password)
    {
        _cachedApiClient.SetLoginCredentials(username, password);
    }

    public async Task<IReadOnlyList<SigenSystemReference>> GetSystemsAsync(CancellationToken cancellationToken = default)
    {
        var systems = await GetSystemListDataAsync(cancellationToken).ConfigureAwait(false);

        return systems
            .Select(system => new SigenSystemReference
            {
                SystemId = RequireSystemField(system.SystemId, "systemId"),
                SystemName = RequireSystemField(system.SystemName, "systemName"),
                TimeZone = system.TimeZone,
            })
            .DistinctBy(system => system.SystemId)
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> GetSystemIdsAsync(CancellationToken cancellationToken = default)
    {
        var systems = await GetSystemsAsync(cancellationToken).ConfigureAwait(false);
        return systems.Select(system => system.SystemId).ToArray();
    }

    public async Task<SigenBatteryStatusSnapshot> GetBatteryStatusAsync(string systemId, CancellationToken cancellationToken = default)
    {
        var requiredSystemId = RequireArgument(systemId, nameof(systemId));

        var energyFlow = await GetEnergyFlowDataAsync(requiredSystemId, cancellationToken).ConfigureAwait(false);

        var batteryStateOfCharge = energyFlow.BatterySoc
            ?? throw new InvalidOperationException("Energy flow data is missing required field 'batterySoc'.");

        var batteryPower = energyFlow.BatteryPower
            ?? throw new InvalidOperationException("Energy flow data is missing required field 'batteryPower'.");

        var systems = await GetSystemListDataAsync(cancellationToken).ConfigureAwait(false);
        var system = systems.FirstOrDefault(item => string.Equals(item.SystemId, requiredSystemId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"System '{requiredSystemId}' was not found in the system list.");

        var onOffGridStatus = RequireSystemField(system.OnOffGridStatus, "onOffGridStatus");

        return new SigenBatteryStatusSnapshot
        {
            BatteryStateOfCharge = batteryStateOfCharge,
            BatteryStatus = MapBatteryStatus(batteryPower),
            IsGridConnected = string.Equals(onOffGridStatus, "onGrid", StringComparison.Ordinal),
        };
    }

    public async Task<SigenPowerFlowSnapshot> GetPowerFlowSnapshotAsync(string systemId, CancellationToken cancellationToken = default)
    {
        var requiredSystemId = RequireArgument(systemId, nameof(systemId));
        var energyFlow = await GetEnergyFlowDataAsync(requiredSystemId, cancellationToken).ConfigureAwait(false);

        return new SigenPowerFlowSnapshot
        {
            PvPower = RequirePowerValue(energyFlow.PvPower, "pvPower"),
            GridPower = RequirePowerValue(energyFlow.GridPower, "gridPower"),
            EvPower = RequirePowerValue(energyFlow.EvPower, "evPower"),
            AcPower = RequirePowerValue(energyFlow.AcPower, "acPower"),
            LoadPower = RequirePowerValue(energyFlow.LoadPower, "loadPower"),
            HeatPumpPower = RequirePowerValue(energyFlow.HeatPumpPower, "heatPumpPower"),
            BatteryPower = RequirePowerValue(energyFlow.BatteryPower, "batteryPower"),
        };
    }

    public async Task<SigenHistoricalComparisonSnapshot> GetHistoricalDayComparisonAsync(string systemId, CancellationToken cancellationToken = default)
    {
        var requiredSystemId = RequireArgument(systemId, nameof(systemId));

        var systems = await GetSystemListDataAsync(cancellationToken).ConfigureAwait(false);
        var system = systems.FirstOrDefault(item => string.Equals(item.SystemId, requiredSystemId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"System '{requiredSystemId}' was not found in the system list.");

        var todayDate = GetSystemTodayDate(system.TimeZone);
        var yesterdayDate = todayDate.AddDays(-1);

        var todayTask = GetHistoricalV1DayDataAsync(requiredSystemId, todayDate, DefaultCachePeriod, cancellationToken);
        var yesterdayTask = GetHistoricalV1DayDataAsync(requiredSystemId, yesterdayDate, HistoricalYesterdayCachePeriod, cancellationToken);
        var todaySocTask = GetDayStateOfChargeAsync(requiredSystemId, todayDate, DefaultCachePeriod, cancellationToken);
        var yesterdaySocTask = GetDayStateOfChargeAsync(requiredSystemId, yesterdayDate, HistoricalYesterdayCachePeriod, cancellationToken);

        await Task.WhenAll(todayTask, yesterdayTask, todaySocTask, yesterdaySocTask).ConfigureAwait(false);

        var today = await todayTask.ConfigureAwait(false);
        var yesterday = await yesterdayTask.ConfigureAwait(false);

        var rows = BuildStationCardRows(today.StationCards, yesterday.StationCards);

        return new SigenHistoricalComparisonSnapshot
        {
            TodayDate = todayDate,
            YesterdayDate = yesterdayDate,
            StationCards = rows,
            TodayChartData = SerializeToJsonElement(today.ChartData),
            YesterdayChartData = SerializeToJsonElement(yesterday.ChartData),
            TodayStateOfCharge = await todaySocTask.ConfigureAwait(false),
            YesterdayStateOfCharge = await yesterdaySocTask.ConfigureAwait(false),
        };
    }

    private async Task<IReadOnlyList<SigenStateOfChargePoint>> GetDayStateOfChargeAsync(
        string systemId,
        DateOnly date,
        TimeSpan cachePeriod,
        CancellationToken cancellationToken)
    {
        var envelope = await _cachedApiClient
            .GetSystemDayHistoryAsync(systemId, HistoricalLevelDay, date, cachePeriod, cancellationToken)
            .ConfigureAwait(false);

        ValidateEnvelope(envelope, "Failed to get battery state of charge history.");

        if (string.IsNullOrWhiteSpace(envelope.Data))
        {
            return [];
        }

        var data = JsonSerializer.Deserialize<SigenHistoryDayData>(envelope.Data);
        return (data?.ItemList ?? [])
            .Where(item => item.BatSoc.HasValue
                && DateTime.TryParseExact(item.DataTime, "yyyyMMdd HH:mm", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _))
            .Select(item => new SigenStateOfChargePoint
            {
                Time = DateTime.ParseExact(item.DataTime!, "yyyyMMdd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                StateOfCharge = item.BatSoc!.Value,
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<SigenSystemInfo>> GetSystemListDataAsync(CancellationToken cancellationToken)
    {
        var envelope = await _cachedApiClient
            .GetSystemListAsync(DefaultCachePeriod, cancellationToken)
            .ConfigureAwait(false);

        ValidateEnvelope(envelope, "Failed to get system list.");

        if (string.IsNullOrWhiteSpace(envelope.Data))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<SigenSystemInfo>>(envelope.Data)
            ?? throw new InvalidOperationException("System list payload was empty.");
    }

    private async Task<SigenEnergyFlowData> GetEnergyFlowDataAsync(string systemId, CancellationToken cancellationToken)
    {
        var envelope = await _cachedApiClient
            .GetSystemEnergyFlowAsync(systemId, DefaultCachePeriod, cancellationToken)
            .ConfigureAwait(false);

        ValidateEnvelope(envelope, "Failed to get energy flow data.");

        if (string.IsNullOrWhiteSpace(envelope.Data))
        {
            throw new InvalidOperationException("Energy flow data payload was empty.");
        }

        return JsonSerializer.Deserialize<SigenEnergyFlowData>(envelope.Data)
            ?? throw new InvalidOperationException("Energy flow data payload could not be parsed.");
    }

    private async Task<SigenHistoricalV1Data> GetHistoricalV1DayDataAsync(
        string systemId,
        DateOnly date,
        TimeSpan cachePeriod,
        CancellationToken cancellationToken)
    {
        var envelope = await _cachedApiClient
            .GetSystemHistoricalDataV1Async(systemId, HistoricalLevelDay, date, cachePeriod, cancellationToken)
            .ConfigureAwait(false);

        ValidateEnvelope(envelope, "Failed to get historical day data.");

        if (string.IsNullOrWhiteSpace(envelope.Data))
        {
            throw new InvalidOperationException("Historical day data payload was empty.");
        }

        return JsonSerializer.Deserialize<SigenHistoricalV1Data>(envelope.Data)
            ?? throw new InvalidOperationException("Historical day data payload could not be parsed.");
    }

    private static DateOnly GetSystemTodayDate(string? systemTimeZone)
    {
        if (string.IsNullOrWhiteSpace(systemTimeZone))
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(systemTimeZone);
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
            return DateOnly.FromDateTime(now.DateTime);
        }
        catch
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }
    }

    private static IReadOnlyList<SigenStationCardComparisonRow> BuildStationCardRows(
        IReadOnlyList<SigenStationCard>? todayCards,
        IReadOnlyList<SigenStationCard>? yesterdayCards)
    {
        var todayMap = (todayCards ?? [])
            .Where(card => !string.IsNullOrWhiteSpace(card.CardKey))
            .ToDictionary(card => card.CardKey!, card => card.Value ?? 0m, StringComparer.Ordinal);

        var yesterdayMap = (yesterdayCards ?? [])
            .Where(card => !string.IsNullOrWhiteSpace(card.CardKey))
            .ToDictionary(card => card.CardKey!, card => card.Value ?? 0m, StringComparer.Ordinal);

        var keys = todayMap.Keys
            .Union(yesterdayMap.Keys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        return keys.Select(key => new SigenStationCardComparisonRow
        {
            CardKey = key,
            TodayKwh = todayMap.GetValueOrDefault(key, 0m),
            YesterdayKwh = yesterdayMap.GetValueOrDefault(key, 0m),
        }).ToArray();
    }

    private static JsonElement SerializeToJsonElement<T>(T value)
    {
        return JsonSerializer.SerializeToElement(value);
    }

    private static BatteryStatus MapBatteryStatus(decimal batPower)
    {
        if (batPower > 0)
        {
            return BatteryStatus.Charging;
        }

        if (batPower < 0)
        {
            return BatteryStatus.Discharging;
        }

        return BatteryStatus.Idle;
    }

    private static string RequireArgument(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value;
    }

    private static string RequireSystemField(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"System list item is missing required field '{fieldName}'.");
        }

        return value;
    }

    private static decimal RequirePowerValue(decimal? value, string fieldName)
    {
        if (!value.HasValue)
        {
            throw new InvalidOperationException($"Energy flow data is missing required field '{fieldName}'.");
        }

        return value.Value;
    }

    private static void ValidateEnvelope<T>(SigenApiEnvelope<T> envelope, string fallbackMessage)
    {
        if (envelope.Code.HasValue && envelope.Code.Value != 0)
        {
            throw new InvalidOperationException(envelope.Message ?? envelope.Msg ?? fallbackMessage);
        }

        if (envelope.Success == false)
        {
            throw new InvalidOperationException(envelope.Message ?? envelope.Msg ?? fallbackMessage);
        }
    }
}
