using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SigCompanion.Lib.EnergyPrices;

public sealed partial class AmberEnergyPriceProvider : IEnergyPriceProvider
{
    private const string BaseUrl = "https://api.amber.com.au/v1";
    private readonly HttpClient _httpClient;
    private readonly AuthenticationHeaderValue _authorization;
    private readonly SemaphoreSlim _siteLock = new(1, 1);
    private string? _siteId;

    public AmberEnergyPriceProvider(HttpClient httpClient, string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        _httpClient = httpClient;
        _authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public CultureInfo CurrencyCulture => CultureInfo.GetCultureInfo("en-AU");
    public string MinorUnitSuffix => "c";

    public async Task<EnergyPriceSnapshot> GetPricesAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var siteId = await GetSiteIdAsync(cancellationToken).ConfigureAwait(false);
        var sitePath = $"sites/{Uri.EscapeDataString(siteId)}";
        var prices = await GetAsync($"{sitePath}/prices/current",
                AmberJsonContext.Default.CurrentPriceArray, cancellationToken)
            .ConfigureAwait(false);
        var buy = prices.FirstOrDefault(price => price.ChannelType == "general");
        var sell = prices.FirstOrDefault(price => price.ChannelType == "feedIn");
        if (buy is null || sell is null)
        {
            throw new InvalidOperationException("Amber did not return both general and feed-in prices.");
        }

        var usagePath = string.Create(CultureInfo.InvariantCulture,
            $"{sitePath}/usage?startDate={today:yyyy-MM-dd}&endDate={today.AddDays(1):yyyy-MM-dd}");
        var usage = await GetAsync(usagePath, AmberJsonContext.Default.UsageIntervalArray, cancellationToken)
            .ConfigureAwait(false);

        return new EnergyPriceSnapshot(ToTariff(buy, "General usage", buy.PerKwh),
            ToTariff(sell, "Feed-in", -sell.PerKwh), usage.Sum(interval => interval.Cost));
    }

    private async Task<string> GetSiteIdAsync(CancellationToken cancellationToken)
    {
        await _siteLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_siteId is not null)
            {
                return _siteId;
            }

            var sites = await GetAsync("sites", AmberJsonContext.Default.SiteArray, cancellationToken).ConfigureAwait(false);
            var siteId = sites.FirstOrDefault()?.Id;
            if (string.IsNullOrWhiteSpace(siteId))
            {
                throw new InvalidOperationException("Amber did not return a valid first site ID.");
            }

            return _siteId = siteId;
        }
        finally
        {
            _siteLock.Release();
        }
    }

    private async Task<T[]> GetAsync<T>(string path, JsonTypeInfo<T[]> typeInfo, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/{path}");
        request.Headers.Authorization = _authorization;
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Amber returned an empty response.");
    }

    private static EnergyTariff ToTariff(CurrentPrice price, string tariffName, decimal centsPerKwh)
    {
        var (name, level) = price.Descriptor switch
        {
            "extremelyLow" => ("Ext Low", 1),
            "veryLow" => ("Very Low", 2),
            "low" => ("Low", 3),
            "neutral" => ("Neutral", 4),
            "high" => ("High", 5),
            "spike" => ("Spike", 6),
            _ => (price.Descriptor, 0),
        };

        return new EnergyTariff(centsPerKwh, tariffName, name, level);
    }

    private sealed record Site([property: JsonRequired] string Id);

    private sealed record CurrentPrice(
        [property: JsonRequired] string ChannelType,
        [property: JsonRequired] decimal PerKwh,
        [property: JsonRequired] string Descriptor);

    private sealed record UsageInterval([property: JsonRequired] decimal Cost);

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(Site[]))]
    [JsonSerializable(typeof(CurrentPrice[]))]
    [JsonSerializable(typeof(UsageInterval[]))]
    private sealed partial class AmberJsonContext : JsonSerializerContext;
}
