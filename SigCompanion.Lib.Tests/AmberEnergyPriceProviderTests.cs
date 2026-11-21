using System.Globalization;
using SigCompanion.Lib.EnergyPrices;

namespace SigCompanion.Lib.Tests;

public sealed class AmberEnergyPriceProviderTests
{
    [Fact]
    public async Task RefreshUsesCachedFirstSiteAndReturnsRatesAndSignedUsageTotal()
    {
        var requests = new List<string>();
        using var client = new HttpClient(new StubHttpHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("api.amber.com.au", request.RequestUri!.Host);
            Assert.Equal("Bearer test-key", request.Headers.Authorization?.ToString());
            var path = request.RequestUri.PathAndQuery;
            requests.Add(path);
            return request.RequestUri.AbsolutePath switch
            {
                "/v1/sites" => """[{"id":"first"},{"id":"second"}]""",
                "/v1/sites/first/prices/current" when requests.Count == 2 =>
                    """[{"channelType":"feedIn","perKwh":-8.75349,"descriptor":"high"},{"channelType":"general","perKwh":17.8818,"descriptor":"veryLow"}]""",
                "/v1/sites/first/prices/current" =>
                    """[{"channelType":"general","perKwh":20,"descriptor":"unmappedValue"},{"channelType":"feedIn","perKwh":-9,"descriptor":"high"}]""",
                "/v1/sites/first/usage" => UsageResponse(request.RequestUri.Query),
                _ => throw new InvalidOperationException($"Unexpected request: {path}"),
            };
        }));
        var provider = new AmberEnergyPriceProvider(client, "test-key");

        var firstDate = DateTime.Today;
        var first = await provider.GetPricesAsync();
        var firstEndDate = DateTime.Today;
        var secondDate = DateTime.Today;
        var second = await provider.GetPricesAsync();

        Assert.Equal(5, requests.Count);
        Assert.Equal("/v1/sites", requests[0]);
        Assert.Equal("/v1/sites/first/prices/current", requests[1]);
        Assert.Equal("/v1/sites/first/prices/current", requests[3]);
        Assert.Contains(requests[2], new[] { UsagePath(firstDate), UsagePath(firstEndDate) });
        Assert.Contains(requests[4], new[] { UsagePath(secondDate), UsagePath(DateTime.Today) });
        Assert.Equal((17.8818m, "Very Low", 2), (first.Buy.CentsPerKwh, first.Buy.PriceLevelName, first.Buy.PriceLevel));
        Assert.Equal((8.75349m, "High", 5), (first.Sell.CentsPerKwh, first.Sell.PriceLevelName, first.Sell.PriceLevel));
        Assert.Equal((20m, "unmappedValue", 0), (second.Buy.CentsPerKwh, second.Buy.PriceLevelName, second.Buy.PriceLevel));
        Assert.Equal(2.85m, first.TodayTotalDollars);
        Assert.Equal(2.85m, second.TodayTotalDollars);
        Assert.Equal(-0.75m, first.YesterdayTotalDollars);
        Assert.Equal(-0.75m, second.YesterdayTotalDollars);
    }

    private static string UsagePath(DateTime date) => string.Create(CultureInfo.InvariantCulture,
        $"/v1/sites/first/usage?startDate={date.AddDays(-1):yyyy-MM-dd}&endDate={date.AddDays(1):yyyy-MM-dd}");

    private static string UsageResponse(string query)
    {
        var yesterday = DateOnly.ParseExact(query[11..21], "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return string.Create(CultureInfo.InvariantCulture, $$"""
            [{"date":"{{yesterday:yyyy-MM-dd}}","cost":-1.00},
             {"date":"{{yesterday.AddDays(1):yyyy-MM-dd}}","cost":3.25},
             {"date":"{{yesterday.AddDays(1):yyyy-MM-dd}}","cost":-0.65},
             {"date":"{{yesterday.AddDays(1):yyyy-MM-dd}}","cost":0.25},
             {"date":"{{yesterday:yyyy-MM-dd}}","cost":0.25},
             {"date":"{{yesterday.AddDays(2):yyyy-MM-dd}}","cost":99}]
            """);
    }
}
