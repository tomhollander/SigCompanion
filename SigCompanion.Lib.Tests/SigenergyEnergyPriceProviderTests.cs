using SigCompanion.Lib.EnergyPrices;
using SigCompanion.Lib.Sigen;
using SigCompanion.Lib.Sigen.Models;

namespace SigCompanion.Lib.Tests;

public sealed class SigenergyEnergyPriceProviderTests
{
    [Fact]
    public async Task RefreshCachesStationAndSelectsLastPriceStrictlyBeforeNow()
    {
        var requests = new List<string>();
        using var client = new HttpClient(new StubHttpHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("Bearer test-token", request.Headers.Authorization?.ToString());
            var path = request.RequestUri!.PathAndQuery;
            requests.Add(path);
            return path switch
            {
                "/device/owner/station/home" =>
                    """{"code":0,"msg":"success","data":{"stationId":2611171411291222,"countryCode":"GB"}}""",
                "/electricity-price/api/price-preview/home/2611171411291222?dt=2026-11-21" => """
                    {"code":0,"msg":"success","data":{"pricePoints":[
                        {"localTime":"2026-11-21T00:00:00","buyPrice":0.10,"sellPrice":0.02},
                        {"localTime":"2026-11-21T00:05:00","buyPrice":0.08,"sellPrice":0.03},
                        {"localTime":"2026-11-21T00:10:00","buyPrice":0.20,"sellPrice":0.04},
                        {"localTime":"2026-11-21T00:15:00","buyPrice":0.99,"sellPrice":0.99}
                    ]}}
                    """,
                _ => throw new InvalidOperationException($"Unexpected request: {path}"),
            };
        }));
        var api = new SigenUiApi(new SigenCachedApiClient(new SigenApiClient(client)));
        api.SetCredentials(new SigenCredentials { AccessToken = "test-token" });
        var provider = Assert.IsType<SigenergyEnergyPriceProvider>(
            EnergyPriceProviderFactory.Create(EnergyPriceProviderFactory.Sigenergy, api));
        var now = new DateTime(2026, 11, 21, 0, 10, 0);

        var first = await provider.GetPricesAsync(null, now);
        var second = await provider.GetPricesAsync(null, now.AddSeconds(1));

        Assert.Equal(3, requests.Count);
        Assert.Equal("/device/owner/station/home", requests[0]);
        Assert.Equal(requests[1], requests[2]);
        Assert.Equal(8m, first.Buy.CentsPerKwh);
        Assert.Equal(3m, first.Sell.CentsPerKwh);
        Assert.Empty(first.Buy.PriceLevelName);
        Assert.Null(first.TodayTotalDollars);
        Assert.Equal(20m, second.Buy.CentsPerKwh);
        Assert.Equal("en-GB", provider.CurrencyCulture.Name);
        Assert.Equal("p", provider.MinorUnitSuffix);
    }
}
