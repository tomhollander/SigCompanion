using SigCompanion.Lib.Sigen;

namespace SigCompanion.Lib.EnergyPrices;

public static class EnergyPriceProviderFactory
{
    public const string Sigenergy = "Sigenergy";

    public static IReadOnlyList<string> SupportedProviders { get; } = Array.AsReadOnly(new[] { Sigenergy });

    public static IEnergyPriceProvider Create(string provider, SigenUiApi sigenApi) => provider switch
    {
        Sigenergy => new SigenergyEnergyPriceProvider(sigenApi),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported energy price provider."),
    };
}
