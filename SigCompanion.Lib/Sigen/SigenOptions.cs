namespace SigCompanion.Lib.Sigen;

public sealed record SigenOptions
{
    public const string DefaultRegion = "aus";
    public const string DefaultBaseUrl = "https://api-aus.sigencloud.com";
    public const string DefaultLoginPath = "/openapi/auth/login/password";

    public string Region { get; init; } = DefaultRegion;
    public string BaseUrl { get; init; } = DefaultBaseUrl;
    public string LoginPath { get; init; } = DefaultLoginPath;
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(20);
    public TimeSpan AccessTokenExpirySkew { get; init; } = TimeSpan.FromMinutes(1);
}

public sealed record SigenRegionInfo(string Name, string Region, string BaseUrl);

public static class SigenRegions
{
    public static readonly IReadOnlyList<SigenRegionInfo> All =
    [
        new("Australia", "aus", "https://api-aus.sigencloud.com"),
        new("Europe", "eu", "https://api-eu.sigencloud.com"),
        new("Asia", "apac", "https://api-apac.sigencloud.com"),
        new("North America", "us", "https://api-us.sigencloud.com"),
        new("China", "cn", "https://api-cn.sigencloud.com"),
    ];

    public static SigenRegionInfo Default => All[0];

    public static SigenRegionInfo GetByRegion(string? region) =>
        All.FirstOrDefault(item => string.Equals(item.Region, region, StringComparison.OrdinalIgnoreCase)) ?? Default;
}
