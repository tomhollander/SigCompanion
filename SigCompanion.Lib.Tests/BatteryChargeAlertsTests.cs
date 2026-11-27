using SigCompanion.Lib.Sigen;

namespace SigCompanion.Lib.Tests;

public sealed class BatteryChargeAlertsTests
{
    [Fact]
    public void FullAlertRearmsOnlyBelowNinetyFivePercent()
    {
        var alerts = new BatteryChargeAlerts();

        Assert.Null(alerts.Update(99.79m, enabled: true));
        Assert.Equal(BatteryChargeAlert.Full, alerts.Update(99.8m, enabled: true));
        Assert.Null(alerts.Update(100m, enabled: true));
        Assert.Null(alerts.Update(97m, enabled: true));
        Assert.Null(alerts.Update(100m, enabled: true));
        Assert.Null(alerts.Update(95m, enabled: true));
        Assert.Null(alerts.Update(100m, enabled: true));
        Assert.Null(alerts.Update(94.99m, enabled: true));
        Assert.Equal(BatteryChargeAlert.Full, alerts.Update(100m, enabled: true));
    }

    [Fact]
    public void EmptyAlertRearmsOnlyAboveFivePercent()
    {
        var alerts = new BatteryChargeAlerts();

        Assert.Null(alerts.Update(0.21m, enabled: true));
        Assert.Equal(BatteryChargeAlert.Empty, alerts.Update(0.2m, enabled: true));
        Assert.Null(alerts.Update(0m, enabled: true));
        Assert.Null(alerts.Update(3m, enabled: true));
        Assert.Null(alerts.Update(0m, enabled: true));
        Assert.Null(alerts.Update(5m, enabled: true));
        Assert.Null(alerts.Update(0m, enabled: true));
        Assert.Null(alerts.Update(5.01m, enabled: true));
        Assert.Equal(BatteryChargeAlert.Empty, alerts.Update(0m, enabled: true));
    }

    [Fact]
    public void DisabledAlertsDoNotPlayAndCanStillRearm()
    {
        var alerts = new BatteryChargeAlerts();

        Assert.Null(alerts.Update(100m, enabled: false));
        Assert.Equal(BatteryChargeAlert.Full, alerts.Update(100m, enabled: true));
        Assert.Null(alerts.Update(100m, enabled: false));
        Assert.Null(alerts.Update(100m, enabled: true));
        Assert.Null(alerts.Update(94m, enabled: false));
        Assert.Equal(BatteryChargeAlert.Full, alerts.Update(100m, enabled: true));
    }

    [Fact]
    public void OppositeExtremesEachPlayTheirOwnAlert()
    {
        var alerts = new BatteryChargeAlerts();

        Assert.Equal(BatteryChargeAlert.Empty, alerts.Update(0m, enabled: true));
        Assert.Equal(BatteryChargeAlert.Full, alerts.Update(100m, enabled: true));
        Assert.Equal(BatteryChargeAlert.Empty, alerts.Update(0m, enabled: true));
    }
}
