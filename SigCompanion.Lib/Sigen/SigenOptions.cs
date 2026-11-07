namespace SigCompanion.Lib.Sigen;

public sealed class SigenOptions
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
