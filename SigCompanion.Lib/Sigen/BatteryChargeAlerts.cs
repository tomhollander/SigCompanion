namespace SigCompanion.Lib.Sigen;

public enum BatteryChargeAlert
{
    Empty,
    Full
}

public sealed class BatteryChargeAlerts
{
    private bool _emptyAlerted;
    private bool _fullAlerted;

    public BatteryChargeAlert? Update(decimal stateOfCharge, bool enabled)
    {
        if (stateOfCharge is < 0m or > 100m)
        {
            return null;
        }

        if (stateOfCharge > 5m)
        {
            _emptyAlerted = false;
        }

        if (stateOfCharge < 95m)
        {
            _fullAlerted = false;
        }

        if (!enabled)
        {
            return null;
        }

        if (!_emptyAlerted && stateOfCharge <= 0.2m)
        {
            _emptyAlerted = true;
            return BatteryChargeAlert.Empty;
        }

        if (!_fullAlerted && stateOfCharge >= 99.8m)
        {
            _fullAlerted = true;
            return BatteryChargeAlert.Full;
        }

        return null;
    }
}
