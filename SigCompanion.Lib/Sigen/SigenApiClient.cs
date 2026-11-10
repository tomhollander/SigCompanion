using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SigCompanion.Lib.Sigen.Models;

namespace SigCompanion.Lib.Sigen;

public sealed class SigenApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly object _sync = new();
    private const string UserAgent = "SigCompanion/1.0";

    private SigenCredentials? _credentials;
    private SigenLoginContext? _loginContext;

    public SigenApiClient(HttpClient httpClient, SigenOptions? options = null, JsonSerializerOptions? serializerOptions = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Options = options ?? new SigenOptions();
        _serializerOptions = serializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public SigenOptions Options { get; private set; }

    public void SetRegion(SigenRegionInfo region)
    {
        ArgumentNullException.ThrowIfNull(region);

        lock (_sync)
        {
            if (string.Equals(Options.BaseUrl, region.BaseUrl, StringComparison.Ordinal))
            {
                return;
            }

            Options = Options with { Region = region.Region, BaseUrl = region.BaseUrl };
            _credentials = null;
        }
    }

    public void SetLoginCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username is required.", nameof(username));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }

        lock (_sync)
        {
            if (_loginContext is not null && !string.Equals(_loginContext.Username, username, StringComparison.OrdinalIgnoreCase))
            {
                _credentials = null;
            }

            _loginContext = new SigenLoginContext
            {
                Username = username,
                Password = password,
            };
        }
    }

    public void SetCredentials(SigenCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        if (string.IsNullOrWhiteSpace(credentials.AccessToken))
        {
            throw new ArgumentException("Access token is required.", nameof(credentials));
        }

        lock (_sync)
        {
            _credentials = credentials;
        }
    }

    public SigenCredentials? GetCredentials()
    {
        lock (_sync)
        {
            return _credentials;
        }
    }

    public async Task<SigenCredentials> LoginAsync(CancellationToken cancellationToken = default)
    {
        var context = GetRequiredLoginContext();
        var request = new SigenLoginRequest
        {
            Username = context.Username,
            Password = context.Password,
        };

        var response = await SendAsync(
            HttpMethod.Post,
            Options.LoginPath,
            body: request,
            includeAuthorization: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var envelope = await DeserializeEnvelopeAsync<JsonElement>(response, cancellationToken).ConfigureAwait(false);

        var tokenPayload = TryParseTokenPayload(envelope.Data);

        if (!response.IsSuccessStatusCode || tokenPayload is null || string.IsNullOrWhiteSpace(tokenPayload.AccessToken))
        {
            var message = envelope.Message ?? envelope.Msg ?? "Sigen login failed.";
            throw new InvalidOperationException(message);
        }

        var credentials = BuildCredentials(tokenPayload);
        SetCredentials(credentials);
        return credentials;
    }

    public async Task<SigenApiEnvelope<string>> GetSystemListAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendAuthorizedAsync(HttpMethod.Get, "/openapi/system", cancellationToken: cancellationToken).ConfigureAwait(false);
        return await DeserializeEnvelopeAsync<string>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SigenApiEnvelope<SigenPagedResult<SigenSystemInfo>>> GetSystemPageAsync(
        int pageNo,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageNo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNo));
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        var query = new Dictionary<string, string>
        {
            ["pageNo"] = pageNo.ToString(),
            ["pageSize"] = pageSize.ToString(),
        };

        var response = await SendAuthorizedAsync(HttpMethod.Get, "/openapi/system/page", query, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await DeserializeEnvelopeAsync<SigenPagedResult<SigenSystemInfo>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SigenApiEnvelope<List<string>>> GetDeviceListAsync(string systemId, CancellationToken cancellationToken = default)
    {
        var path = $"/openapi/system/{Uri.EscapeDataString(Require(systemId, nameof(systemId)))}/devices";
        var response = await SendAuthorizedAsync(HttpMethod.Get, path, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await DeserializeEnvelopeAsync<List<string>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SigenApiEnvelope<SigenSystemRealtimeData>> GetSystemRealtimeDataAsync(string systemId, CancellationToken cancellationToken = default)
    {
        var path = $"/openapi/systems/{Uri.EscapeDataString(Require(systemId, nameof(systemId)))}/summary";
        var response = await SendAuthorizedAsync(HttpMethod.Get, path, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await DeserializeEnvelopeAsync<SigenSystemRealtimeData>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SigenApiEnvelope<string>> GetSystemEnergyFlowAsync(string systemId, CancellationToken cancellationToken = default)
    {
        var path = $"/openapi/systems/{Uri.EscapeDataString(Require(systemId, nameof(systemId)))}/energyFlow";
        var response = await SendAuthorizedAsync(HttpMethod.Get, path, cancellationToken: cancellationToken).ConfigureAwait(false);
        var envelope = await DeserializeEnvelopeAsync<JsonElement>(response, cancellationToken).ConfigureAwait(false);
        return NormalizeJsonDataEnvelopeToString(envelope);
    }

    public async Task<SigenApiEnvelope<SigenHistoricalSeries>> GetSystemHistoricalDataAsync(
        string systemId,
        IReadOnlyDictionary<string, string>? query = null,
        bool useV1Path = false,
        CancellationToken cancellationToken = default)
    {
        var path = useV1Path
            ? $"/openapi/systems/{Uri.EscapeDataString(Require(systemId, nameof(systemId)))}/v1/history"
            : $"/openapi/systems/{Uri.EscapeDataString(Require(systemId, nameof(systemId)))}/history";

        var response = await SendAuthorizedAsync(HttpMethod.Get, path, query, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await DeserializeEnvelopeAsync<SigenHistoricalSeries>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SigenApiEnvelope<string>> GetSystemHistoricalDataV1Async(
        string systemId,
        string level,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var requiredLevel = Require(level, nameof(level));
        var path = $"/openapi/systems/{Uri.EscapeDataString(Require(systemId, nameof(systemId)))}/v1/history";

        var query = new Dictionary<string, string>
        {
            ["level"] = requiredLevel,
            ["date"] = date.ToString("yyyy-MM-dd"),
        };

        var response = await SendAuthorizedAsync(HttpMethod.Get, path, query, cancellationToken: cancellationToken).ConfigureAwait(false);
        var envelope = await DeserializeEnvelopeAsync<JsonElement>(response, cancellationToken).ConfigureAwait(false);
        return NormalizeJsonDataEnvelopeToString(envelope);
    }

    public async Task<SigenApiEnvelope<string>> GetSystemDayHistoryAsync(
        string systemId,
        string level,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var path = $"/openapi/systems/{Uri.EscapeDataString(Require(systemId, nameof(systemId)))}/history";

        var query = new Dictionary<string, string>
        {
            ["level"] = Require(level, nameof(level)),
            ["date"] = date.ToString("yyyy-MM-dd"),
        };

        var response = await SendAuthorizedAsync(HttpMethod.Get, path, query, cancellationToken: cancellationToken).ConfigureAwait(false);
        var envelope = await DeserializeEnvelopeAsync<JsonElement>(response, cancellationToken).ConfigureAwait(false);
        return NormalizeJsonDataEnvelopeToString(envelope);
    }

    public async Task<SigenApiEnvelope<string>> GetDeviceRealtimeDataAsync(
        string systemId,
        string serialNumber,
        CancellationToken cancellationToken = default)
    {
        var path = $"/openapi/systems/{Uri.EscapeDataString(Require(systemId, nameof(systemId)))}/devices/{Uri.EscapeDataString(Require(serialNumber, nameof(serialNumber)))}/realtimeInfo";
        var response = await SendAuthorizedAsync(HttpMethod.Get, path, cancellationToken: cancellationToken).ConfigureAwait(false);
        var envelope = await DeserializeEnvelopeAsync<JsonElement>(response, cancellationToken).ConfigureAwait(false);
        return NormalizeJsonDataEnvelopeToString(envelope);
    }

    public async Task<SigenApiEnvelope<SigenHistoricalSeries>> GetDeviceHistoricalDataAsync(
        string systemId,
        string serialNumber,
        IReadOnlyDictionary<string, string>? query = null,
        CancellationToken cancellationToken = default)
    {
        var path = $"/openapi/systems/{Uri.EscapeDataString(Require(systemId, nameof(systemId)))}/devices/{Uri.EscapeDataString(Require(serialNumber, nameof(serialNumber)))}/history";
        var response = await SendAuthorizedAsync(HttpMethod.Get, path, query, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await DeserializeEnvelopeAsync<SigenHistoricalSeries>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        var credentials = GetCredentials();
        if (credentials is not null && !credentials.IsExpiringSoon(Options.AccessTokenExpirySkew))
        {
            return;
        }

        await LoginAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string>? query = null,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);

        var response = await SendAsync(method, path, query, body, includeAuthorization: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
        {
            return response;
        }

        response.Dispose();
        await LoginAsync(cancellationToken).ConfigureAwait(false);

        return await SendAsync(method, path, query, body, includeAuthorization: true, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string>? query = null,
        object? body = null,
        bool includeAuthorization = true,
        CancellationToken cancellationToken = default)
    {
        var uri = BuildUri(path, query);
        var request = new HttpRequestMessage(method, uri);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd(UserAgent);

        if (includeAuthorization)
        {
            var credentials = GetCredentials() ?? throw new InvalidOperationException("Sigen credentials are not available.");
            request.Headers.Authorization = new AuthenticationHeaderValue(credentials.TokenType, credentials.AccessToken);
        }

        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, _serializerOptions), Encoding.UTF8, "application/json");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(Options.RequestTimeout);

        return await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
    }

    private Uri BuildUri(string path, IReadOnlyDictionary<string, string>? query = null)
    {
        var baseUri = new Uri(Options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        var uriBuilder = new UriBuilder(new Uri(baseUri, path.TrimStart('/')));

        if (query is { Count: > 0 })
        {
            var queryString = string.Join("&", query.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            uriBuilder.Query = queryString;
        }

        return uriBuilder.Uri;
    }

    private static async Task<SigenApiEnvelope<T>> DeserializeEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            using var errorDocument = await TryParseJsonAsync(stream, cancellationToken).ConfigureAwait(false);
            if (IsRateLimitResponse(errorDocument))
            {
                throw new SigenRateLimitException();
            }

            var bodyMessage = errorDocument is not null
                && errorDocument.RootElement.TryGetProperty("msg", out var msgElement)
                && msgElement.ValueKind == JsonValueKind.String
                    ? msgElement.GetString()
                    : null;

            throw new HttpRequestException(
                bodyMessage ?? $"Sigen API request failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
                null,
                response.StatusCode);
        }

        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Check the code before typed deserialization: rate-limit responses return data: {},
        // which cannot be deserialized into types such as string.
        if (IsRateLimitResponse(document))
        {
            throw new SigenRateLimitException();
        }

        var envelope = document.RootElement.Deserialize<SigenApiEnvelope<T>>();

        if (envelope is null)
        {
            throw new InvalidOperationException("Sigen API returned an invalid response.");
        }

        return envelope;
    }

    private static async Task<JsonDocument?> TryParseJsonAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsRateLimitResponse(JsonDocument? document) =>
        document is not null
        && document.RootElement.TryGetProperty("code", out var codeElement)
        && codeElement.ValueKind == JsonValueKind.Number
        && codeElement.GetInt32() == SigenRateLimitException.RateLimitCode;

    private SigenTokenPayload? TryParseTokenPayload(JsonElement tokenData)
    {
        if (tokenData.ValueKind == JsonValueKind.Object)
        {
            return tokenData.Deserialize<SigenTokenPayload>(_serializerOptions);
        }

        if (tokenData.ValueKind == JsonValueKind.String)
        {
            var raw = tokenData.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return JsonSerializer.Deserialize<SigenTokenPayload>(raw, _serializerOptions);
        }

        return null;
    }

    private static SigenApiEnvelope<string> NormalizeJsonDataEnvelopeToString(SigenApiEnvelope<JsonElement> envelope)
    {
        string? data = envelope.Data.ValueKind switch
        {
            JsonValueKind.String => envelope.Data.GetString(),
            JsonValueKind.Object => envelope.Data.GetRawText(),
            JsonValueKind.Array => envelope.Data.GetRawText(),
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => envelope.Data.GetRawText(),
        };

        return new SigenApiEnvelope<string>
        {
            Code = envelope.Code,
            Message = envelope.Message,
            Msg = envelope.Msg,
            Success = envelope.Success,
            Data = data,
            Extra = envelope.Extra,
        };
    }

    private SigenLoginContext GetRequiredLoginContext()
    {
        lock (_sync)
        {
            return _loginContext ?? throw new InvalidOperationException("Login credentials are not set. Call SetLoginCredentials first.");
        }
    }

    private SigenCredentials BuildCredentials(SigenTokenPayload payload)
    {
        return new SigenCredentials
        {
            AccessToken = payload.AccessToken!,
            RefreshToken = payload.RefreshToken,
            TokenType = string.IsNullOrWhiteSpace(payload.TokenType) ? "Bearer" : payload.TokenType,
            Region = Options.Region,
            ExpiresAt = payload.ExpiresIn is > 0
                ? DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn.Value)
                : null,
            Username = payload.Username,
        };
    }

    private static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value;
    }
}
