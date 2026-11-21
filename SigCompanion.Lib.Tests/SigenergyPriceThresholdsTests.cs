using SigCompanion.Lib.EnergyPrices;

namespace SigCompanion.Lib.Tests;

public sealed class SigenergyPriceThresholdsTests
{
    [Theory]
    [InlineData(-1, 1, "Ext Low")]
    [InlineData(0, 2, "Very Low")]
    [InlineData(10, 3, "Low")]
    [InlineData(20, 4, "Medium")]
    [InlineData(30, 5, "High")]
    [InlineData(40, 6, "Very high")]
    [InlineData(70, 7, "Ext High")]
    [InlineData(100, 7, "Ext High")]
    public void BuyDefaultsAssignExactBoundariesToTheHigherBand(int rate, int level, string name)
    {
        var result = SigenergyPriceThresholds.DefaultBuy.Label(new EnergyTariff(rate, "General usage", "", 0));

        Assert.Equal(level, result.PriceLevel);
        Assert.Equal(name, result.PriceLevelName);
        Assert.Equal((decimal)rate, result.CentsPerKwh);
        Assert.Equal("General usage", result.TariffName);
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(6, 4)]
    [InlineData(13, 5)]
    [InlineData(20, 6)]
    [InlineData(33, 7)]
    public void SellDefaultsUseTheirOwnThresholds(int rate, int level)
    {
        var result = SigenergyPriceThresholds.DefaultSell.Label(new EnergyTariff(rate, "Feed-in", "", 0));

        Assert.Equal(level, result.PriceLevel);
    }

    [Fact]
    public void ClassifiesUnroundedRatesUsingFractionalCutoffs()
    {
        var thresholds = new SigenergyPriceThresholds(3, 8);

        Assert.Equal(2, thresholds.Label(new EnergyTariff(1.49m, "", "", 0)).PriceLevel);
        Assert.Equal(3, thresholds.Label(new EnergyTariff(1.50m, "", "", 0)).PriceLevel);
        Assert.Equal(4, thresholds.Label(new EnergyTariff(5.49m, "", "", 0)).PriceLevel);
        Assert.Equal(5, thresholds.Label(new EnergyTariff(5.50m, "", "", 0)).PriceLevel);
    }
}
