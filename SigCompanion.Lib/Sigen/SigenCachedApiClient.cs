using System.Collections.Concurrent;
using System.Text;
using SigCompanion.Lib.Sigen.Models;

namespace SigCompanion.Lib.Sigen;

public sealed class SigenCachedApiClient
{
    private readonly SigenApiClient _innerClient;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

    public SigenCachedApiClient(SigenApiClient innerClient)
    {
        _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
    }

    public void SetLoginCredentials(string username, string password) =>
        _innerClient.SetLoginCredentials(username, password);

    public void SetCredentials(SigenCredentials credentials) =>
        _innerClient.SetCredentials(credentials);

    public SigenCredentials? GetCredentials() =>
        _innerClient.GetCredentials();

    public Task<SigenCredentials> LoginAsync(CancellationToken cancellationToken = default) =>
        _innerClient.LoginAsync(cancellationToken);

    public void ClearCache() => _cache.Clear();

    public bool InvalidateCache(string cacheKey) => _cache.TryRemove(cacheKey, out _);

    public Task<SigenApiEnvelope<string>> GetSystemListAsync(
        TimeSpan cachePeriod,
        CancellationToken cancellationToken = default) =>
        GetOrSetAsync(
            "system:list",
            cachePeriod,
            _innerClient.GetSystemListAsync,
            cancellationToken);

    public Task<SigenApiEnvelope<SigenPagedResult<SigenSystemInfo>>> GetSystemPageAsync(
        int pageNo,
        int pageSize,
        TimeSpan cachePeriod,
        CancellationToken cancellationToken = default) =>
        GetOrSetAsync(
            $"system:page:{pageNo}:{pageSize}",
            cachePeriod,
            token => _innerClient.GetSystemPageAsync(pageNo, pageSize, token),
            cancellationToken);

    public Task<SigenApiEnvelope<List<string>>> GetDeviceListAsync(
        string systemId,
        TimeSpan cachePeriod,
        CancellationToken cancellationToken = default) =>
        GetOrSetAsync(
            $"system:{systemId}:devices",
            cachePeriod,
            token => _innerClient.GetDeviceListAsync(systemId, token),
            cancellationToken);

    public Task<SigenApiEnvelope<SigenSystemRealtimeData>> GetSystemRealtimeDataAsync(
        string systemId,
        TimeSpan cachePeriod,
        CancellationToken cancellationToken = default) =>
        GetOrSetAsync(
            $"system:{systemId}:realtime",
            cachePeriod,
            token => _innerClient.GetSystemRealtimeDataAsync(systemId, token),
            cancellationToken);

    public Task<SigenApiEnvelope<string>> GetSystemEnergyFlowAsync(
        string systemId,
        TimeSpan cachePeriod,
        CancellationToken cancellationToken = default) =>
        GetOrSetAsync(
            $"system:{systemId}:energy-flow",
            cachePeriod,
            token => _innerClient.GetSystemEnergyFlowAsync(systemId, token),
            cancellationToken);

    public Task<SigenApiEnvelope<SigenHistoricalSeries>> GetSystemHistoricalDataAsync(
        string systemId,
        TimeSpan cachePeriod,
        IReadOnlyDictionary<string, string>? query = null,
        bool useV1Path = false,
        CancellationToken cancellationToken = default) =>
        GetOrSetAsync(
            $"system:{systemId}:history:{useV1Path}:{BuildQueryKey(query)}",
            cachePeriod,
            token => _innerClient.GetSystemHistoricalDataAsync(systemId, query, useV1Path, token),
            cancellationToken);

    public Task<SigenApiEnvelope<string>> GetSystemHistoricalDataV1Async(
        string systemId,
        string level,
        DateOnly date,
        TimeSpan cachePeriod,
        CancellationToken cancellationToken = default) =>
        GetOrSetAsync(
            $"system:{systemId}:history:v1:{level}:{date:yyyy-MM-dd}",
            cachePeriod,
            token => _innerClient.GetSystemHistoricalDataV1Async(systemId, level, date, token),
            cancellationToken);

    public Task<SigenApiEnvelope<string>> GetSystemDayHistoryAsync(
        string systemId,
        string level,
        DateOnly date,
        TimeSpan cachePeriod,
        CancellationToken cancellationToken = default) =>
        GetOrSetAsync(
            $"system:{systemId}:history:day:{level}:{date:yyyy-MM-dd}",
            cachePeriod,
            token => _innerClient.GetSystemDayHistoryAsync(systemId, level, date, token),
            cancellationToken);

    public Task<SigenApiEnvelope<string>> GetDeviceRealtimeDataAsync(
        string systemId,
        string serialNumber,
        TimeSpan cachePeriod,
        CancellationToken cancellationToken = default) =>
        GetOrSetAsync(
            $"system:{systemId}:device:{serialNumber}:realtime",
            cachePeriod,
            token => _innerClient.GetDeviceRealtimeDataAsync(systemId, serialNumber, token),
            cancellationToken);

    public Task<SigenApiEnvelope<SigenHistoricalSeries>> GetDeviceHistoricalDataAsync(
        string systemId,
        string serialNumber,
        TimeSpan cachePeriod,
        IReadOnlyDictionary<string, string>? query = null,
        CancellationToken cancellationToken = default) =>
        GetOrSetAsync(
            $"system:{systemId}:device:{serialNumber}:history:{BuildQueryKey(query)}",
            cachePeriod,
            token => _innerClient.GetDeviceHistoricalDataAsync(systemId, serialNumber, query, token),
            cancellationToken);

    private async Task<T> GetOrSetAsync<T>(
        string cacheKey,
        TimeSpan cachePeriod,
        Func<CancellationToken, Task<T>> fetch,
        CancellationToken cancellationToken)
    {
        if (cachePeriod < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(cachePeriod), "Cache period cannot be negative.");
        }

        if (TryGetValidCachedValue(cacheKey, cachePeriod, out T? cached))
        {
            return cached;
        }

        var gate = _keyLocks.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (TryGetValidCachedValue(cacheKey, cachePeriod, out cached))
            {
                return cached;
            }

            var fresh = await fetch(cancellationToken).ConfigureAwait(false);
            _cache[cacheKey] = new CacheEntry(DateTimeOffset.UtcNow, fresh!);
            return fresh;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool TryGetValidCachedValue<T>(string cacheKey, TimeSpan cachePeriod, out T value)
    {
        value = default!;

        if (!_cache.TryGetValue(cacheKey, out var entry))
        {
            return false;
        }

        var age = DateTimeOffset.UtcNow - entry.CachedAtUtc;
        if (age > cachePeriod)
        {
            return false;
        }

        if (entry.Value is not T typed)
        {
            return false;
        }

        value = typed;
        return true;
    }

    private static string BuildQueryKey(IReadOnlyDictionary<string, string>? query)
    {
        if (query is null || query.Count == 0)
        {
            return "none";
        }

        var builder = new StringBuilder();
        foreach (var pair in query.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (builder.Length > 0)
            {
                builder.Append('&');
            }

            builder.Append(pair.Key);
            builder.Append('=');
            builder.Append(pair.Value);
        }

        return builder.ToString();
    }

    private sealed record CacheEntry(DateTimeOffset CachedAtUtc, object Value);
}
