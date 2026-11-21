using SigCompanion.Lib.Sigen;

namespace SigCompanion.Lib.EnergyPrices;

public static class EnergyPriceProviderFactory
{
    public const string Sigenergy = "Sigenergy";
    public const string Amber = "Amber";

    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static IReadOnlyList<string> SupportedProviders { get; } = Array.AsReadOnly(new[] { Sigenergy, Amber });

    public static IEnergyPriceProvider Create(string provider, SigenUiApi sigenApi, string? amberApiKey = null) => provider switch
    {
        Sigenergy => new SigenergyEnergyPriceProvider(sigenApi),
        Amber => new AmberEnergyPriceProvider(HttpClient, amberApiKey ?? throw new ArgumentNullException(nameof(amberApiKey))),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported energy price provider."),
    };
}
