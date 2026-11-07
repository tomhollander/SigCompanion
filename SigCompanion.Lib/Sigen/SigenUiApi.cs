using System.Text.Json;
using SigCompanion.Lib.Sigen.Models;

namespace SigCompanion.Lib.Sigen;

public sealed class SigenUiApi
{
    private static readonly TimeSpan DefaultCachePeriod = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DeviceDiscoveryCachePeriod = TimeSpan.FromHours(24);

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

        var inverterSerialNumber = await GetInverterSerialNumberAsync(requiredSystemId, cancellationToken).ConfigureAwait(false);

        var realtimeEnvelope = await _cachedApiClient
            .GetDeviceRealtimeDataAsync(requiredSystemId, inverterSerialNumber, DefaultCachePeriod, cancellationToken)
            .ConfigureAwait(false);

        ValidateEnvelope(realtimeEnvelope, "Failed to get device realtime data.");

        if (string.IsNullOrWhiteSpace(realtimeEnvelope.Data))
        {
            throw new InvalidOperationException("Device realtime data payload was empty.");
        }

        var realtimeData = JsonSerializer.Deserialize<SigenDeviceRealtimeData>(realtimeEnvelope.Data)
            ?? throw new InvalidOperationException("Device realtime data payload could not be parsed.");

        var realtimeInfo = realtimeData.RealTimeInfo
            ?? throw new InvalidOperationException("Device realtime data is missing required field 'realTimeInfo'.");

        var batteryStateOfCharge = realtimeInfo.BatSoc
            ?? throw new InvalidOperationException("Device realtime data is missing required field 'realTimeInfo.batSoc'.");

        var batteryPower = realtimeInfo.BatPower
            ?? throw new InvalidOperationException("Device realtime data is missing required field 'realTimeInfo.batPower'.");

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

    private async Task<string> GetInverterSerialNumberAsync(string systemId, CancellationToken cancellationToken)
    {
        var envelope = await _cachedApiClient
            .GetDeviceListAsync(systemId, DeviceDiscoveryCachePeriod, cancellationToken)
            .ConfigureAwait(false);

        ValidateEnvelope(envelope, "Failed to get device list.");

        var rawDevices = envelope.Data ?? [];
        var devices = rawDevices
            .Select(ParseDeviceInfo)
            .ToArray();

        var inverter = devices.FirstOrDefault(device => string.Equals(device.DeviceType, "Inverter", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"System '{systemId}' does not contain an inverter device.");

        return RequireDeviceField(inverter.SerialNumber, "serialNumber");
    }

    private static SigenDeviceInfo ParseDeviceInfo(string rawDeviceJson)
    {
        if (string.IsNullOrWhiteSpace(rawDeviceJson))
        {
            throw new InvalidOperationException("Device list item is empty.");
        }

        return JsonSerializer.Deserialize<SigenDeviceInfo>(rawDeviceJson)
            ?? throw new InvalidOperationException("Device list item could not be parsed.");
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

    private static string RequireDeviceField(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Device list item is missing required field '{fieldName}'.");
        }

        return value;
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
