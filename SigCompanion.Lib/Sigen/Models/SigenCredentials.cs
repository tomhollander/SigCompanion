namespace SigCompanion.Lib.Sigen.Models;

public sealed class SigenCredentials
{
    public string AccessToken { get; init; } = string.Empty;
    public string? RefreshToken { get; init; }
    public string TokenType { get; init; } = "Bearer";
    public string Region { get; init; } = SigCompanion.Lib.Sigen.SigenOptions.DefaultRegion;
    public DateTimeOffset? ExpiresAt { get; init; }
    public string? Username { get; init; }

    public bool IsExpiringSoon(TimeSpan threshold) =>
        ExpiresAt.HasValue && ExpiresAt.Value <= DateTimeOffset.UtcNow.Add(threshold);
}
