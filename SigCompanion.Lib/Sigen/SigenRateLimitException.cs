namespace SigCompanion.Lib.Sigen;

/// <summary>
/// Thrown when the Sigen API responds with code 1201 (access restriction / rate limiting).
/// </summary>
public sealed class SigenRateLimitException : InvalidOperationException
{
    public const int RateLimitCode = 1201;

    public SigenRateLimitException()
        : base("The Sigenergy API is rate-limiting requests. Please try again in a few minutes.")
    {
    }
}
