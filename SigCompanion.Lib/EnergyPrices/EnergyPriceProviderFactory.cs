namespace SigCompanion.Lib.EnergyPrices;

public static class EnergyPriceProviderFactory
{
    public const string Amber = "Amber";

    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static IReadOnlyList<string> SupportedProviders { get; } = Array.AsReadOnly(new[] { Amber });

    public static IEnergyPriceProvider Create(string provider, string apiKey) => provider switch
    {
        Amber => new AmberEnergyPriceProvider(HttpClient, apiKey),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported energy price provider."),
    };
}
