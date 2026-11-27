using SigCompanion.Lib.EnergyPrices;
using SigCompanion.Lib.Sigen;
using SigCompanion.Lib.Sigen.Models;

namespace SigCompanion.Lib.Tests;

public sealed class SigenergyEnergyPriceProviderTests
{
    [Fact]
    public void FactoryOnlyRegistersSigenergyAndRejectsRemovedProviders()
    {
        using var client = new HttpClient(new StubHttpHandler(_ => throw new InvalidOperationException("Unexpected request.")));
        var api = new SigenUiApi(new SigenCachedApiClient(new SigenApiClient(client)));

        Assert.Equal(new[] { EnergyPriceProviderFactory.Sigenergy }, EnergyPriceProviderFactory.SupportedProviders);
        Assert.IsType<SigenergyEnergyPriceProvider>(EnergyPriceProviderFactory.Create(EnergyPriceProviderFactory.Sigenergy, api));
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyPriceProviderFactory.Create("Amber", api));
    }

    [Fact]
    public async Task RefreshGetsFreshTodayBillAndRefetchesYesterdayBillAfterCacheExpires()
    {
        const string pricePath = "/electricity-price/api/price-preview/home/123?dt=2026-11-21";
        const string todayBillPath = "/data-process/sigen/station/statistics/bill?stationId=123&dateFlag=1&startDate=20261121&endDate=20261121";
        const string yesterdayBillPath = "/data-process/sigen/station/statistics/bill?stationId=123&dateFlag=1&startDate=20261120&endDate=20261120";
        var requests = new List<string>();
        var todayBillRequests = 0;
        var yesterdayBillRequests = 0;
        using var client = new HttpClient(new StubHttpHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("Bearer test-token", request.Headers.Authorization?.ToString());
            var path = request.RequestUri!.PathAndQuery;
            requests.Add(path);
            return path switch
            {
                "/device/owner/station/home" => """{"code":0,"data":{"stationId":123,"countryCode":"AU"}}""",
                pricePath => """{"code":0,"data":{"pricePoints":[{"localTime":"2026-11-21T00:00:00","buyPrice":0.12,"sellPrice":0.04}]}}""",
                todayBillPath => ++todayBillRequests == 1
                    ? """{"code":0,"data":{"stationId":123,"total":-0.05,"saleRevenue":0.04,"purchaseExpense":0.09,"fixedDailyFee":0,"dataSeries":[]}}"""
                    : """{"code":0,"data":{"total":-0.08}}""",
                yesterdayBillPath => ++yesterdayBillRequests == 1
                    ? """{"code":0,"data":{"total":0.20}}"""
                    : """{"code":0,"data":{"total":0.35}}""",
                _ => throw new InvalidOperationException($"Unexpected request: {path}"),
            };
        }));
        var clock = new ManualTimeProvider { UtcNow = new DateTimeOffset(2026, 11, 20, 13, 10, 0, TimeSpan.Zero) };
        var api = new SigenUiApi(new SigenCachedApiClient(new SigenApiClient(client), clock));
        api.SetCredentials(new SigenCredentials { AccessToken = "test-token" });
        var provider = new SigenergyEnergyPriceProvider(api);
        var now = new DateTime(2026, 11, 21, 0, 10, 0);

        var first = await provider.GetPricesAsync(now);
        clock.UtcNow += TimeSpan.FromMinutes(2);
        var second = await provider.GetPricesAsync(now.AddMinutes(2));
        clock.UtcNow += TimeSpan.FromMinutes(29);
        var third = await provider.GetPricesAsync(now.AddMinutes(31));

        Assert.Equal(0.05m, first.TodayTotalDollars);
        Assert.Equal(-0.20m, first.YesterdayTotalDollars);
        Assert.Equal(0.08m, second.TodayTotalDollars);
        Assert.Equal(first.YesterdayTotalDollars, second.YesterdayTotalDollars);
        Assert.Equal(-0.35m, third.YesterdayTotalDollars);
        Assert.Equal(new[] { "/device/owner/station/home", pricePath, todayBillPath, yesterdayBillPath,
            pricePath, todayBillPath, pricePath, todayBillPath, yesterdayBillPath }, requests);
    }

    [Fact]
    public async Task RefreshSelectsLastPriceStrictlyBeforeNowAndRetriesMissingYesterdayBill()
    {
        var yesterdayBillRequests = 0;
        using var client = new HttpClient(new StubHttpHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("Bearer test-token", request.Headers.Authorization?.ToString());
            var path = request.RequestUri!.PathAndQuery;
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
                "/data-process/sigen/station/statistics/bill?stationId=2611171411291222&dateFlag=1&startDate=20261121&endDate=20261121" =>
                    """{"code":0,"data":{"total":0}}""",
                "/data-process/sigen/station/statistics/bill?stationId=2611171411291222&dateFlag=1&startDate=20261120&endDate=20261120" =>
                    ++yesterdayBillRequests == 1
                        ? """{"code":0,"data":{}}"""
                        : """{"code":0,"data":{"total":0.10}}""",
                _ => throw new InvalidOperationException($"Unexpected request: {path}"),
            };
        }));
        var api = new SigenUiApi(new SigenCachedApiClient(new SigenApiClient(client)));
        api.SetCredentials(new SigenCredentials { AccessToken = "test-token" });
        var provider = Assert.IsType<SigenergyEnergyPriceProvider>(
            EnergyPriceProviderFactory.Create(EnergyPriceProviderFactory.Sigenergy, api));
        var now = new DateTime(2026, 11, 21, 0, 10, 0);

        var first = await provider.GetPricesAsync(now);
        var second = await provider.GetPricesAsync(now.AddSeconds(1));

        Assert.Equal(8m, first.Buy.CentsPerKwh);
        Assert.Equal(3m, first.Sell.CentsPerKwh);
        Assert.Empty(first.Buy.PriceLevelName);
        Assert.Equal(0m, first.TodayTotalDollars);
        Assert.Null(first.YesterdayTotalDollars);
        Assert.Equal(20m, second.Buy.CentsPerKwh);
        Assert.Equal(-0.10m, second.YesterdayTotalDollars);
        Assert.Equal(2, yesterdayBillRequests);
        Assert.Equal("en-GB", provider.CurrencyCulture.Name);
        Assert.Equal("p", provider.MinorUnitSuffix);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; }

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
