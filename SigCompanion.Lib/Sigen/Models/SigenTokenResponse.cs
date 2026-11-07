using System.Text.Json;
using System.Text.Json.Serialization;

namespace SigCompanion.Lib.Sigen.Models;

public sealed class SigenLoginRequest
{
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;
}

public sealed class SigenLoginContext
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed class SigenApiEnvelope<T>
{
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("msg")]
    public string? Msg { get; init; }

    [JsonPropertyName("success")]
    public bool? Success { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

public sealed class SigenTokenPayload
{
    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("tokenType")]
    public string? TokenType { get; init; }

    [JsonPropertyName("expiresIn")]
    public int? ExpiresIn { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}
