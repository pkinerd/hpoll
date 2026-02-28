using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Core.Models;

namespace Hpoll.Core.Services;

public class HueApiClient : IHueApiClient
{
    private const string ClipV2BaseUrl = "https://api.meethue.com/route/clip/v2";
    private const string RemoteApiBaseUrl = "https://api.meethue.com/route";
    private const string TokenUrl = "https://api.meethue.com/v2/oauth2/token";
    private const string HttpClientName = "HueApi";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HueAppSettings _hueAppSettings;
    private readonly ILogger<HueApiClient> _logger;

    public HueApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<HueAppSettings> hueAppSettings,
        ILogger<HueApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _hueAppSettings = hueAppSettings.Value;
        _logger = logger;
    }

    public async Task<HueResponse<HueMotionResource>> GetMotionSensorsAsync(
        string accessToken, string applicationKey, CancellationToken ct = default)
    {
        return await GetResourceAsync<HueMotionResource>("/resource/motion", accessToken, applicationKey, ct);
    }

    public async Task<HueResponse<HueTemperatureResource>> GetTemperatureSensorsAsync(
        string accessToken, string applicationKey, CancellationToken ct = default)
    {
        return await GetResourceAsync<HueTemperatureResource>("/resource/temperature", accessToken, applicationKey, ct);
    }

    public async Task<HueResponse<HueDeviceResource>> GetDevicesAsync(
        string accessToken, string applicationKey, CancellationToken ct = default)
    {
        return await GetResourceAsync<HueDeviceResource>("/resource/device", accessToken, applicationKey, ct);
    }

    public async Task<HueResponse<HueDevicePowerResource>> GetDevicePowerAsync(
        string accessToken, string applicationKey, CancellationToken ct = default)
    {
        return await GetResourceAsync<HueDevicePowerResource>("/resource/device_power", accessToken, applicationKey, ct);
    }

    public async Task<HueTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        return await PostTokenRequestAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        }, ct);
    }

    public async Task<HueTokenResponse> ExchangeAuthorizationCodeAsync(string code, string redirectUri, CancellationToken ct = default)
    {
        return await PostTokenRequestAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri
        }, ct);
    }

    public async Task EnableLinkButtonAsync(string accessToken, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"{RemoteApiBaseUrl}/api/0/config");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { linkbutton = true }),
            Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Enable link button failed with status {StatusCode}: {Body}",
                (int)response.StatusCode, errorBody.Length > 500 ? errorBody[..500] : errorBody);
            throw new HttpRequestException(
                $"Enable link button failed with status {(int)response.StatusCode}",
                null, response.StatusCode);
        }
    }

    public async Task<string> RegisterApplicationAsync(string accessToken, string deviceType = "hpoll", CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{RemoteApiBaseUrl}/api");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { devicetype = deviceType }),
            Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, ct);

        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Register application failed with status {StatusCode}: {Body}",
                (int)response.StatusCode, json.Length > 500 ? json[..500] : json);
            throw new HttpRequestException(
                $"Register application failed with status {(int)response.StatusCode}",
                null, response.StatusCode);
        }

        // v1-style response: [{"success":{"username":"..."}}]
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            var first = root[0];
            if (first.TryGetProperty("success", out var success) &&
                success.TryGetProperty("username", out var username))
            {
                return username.GetString()
                    ?? throw new InvalidOperationException("Username was null in registration response.");
            }
        }

        throw new InvalidOperationException($"Unexpected registration response format: {json}");
    }

    public async Task<string> GetBridgeIdAsync(string accessToken, string applicationKey, CancellationToken ct = default)
    {
        var result = await GetResourceAsync<HueBridgeResource>("/resource/bridge", accessToken, applicationKey, ct);
        if (result.Data.Count == 0)
            throw new InvalidOperationException("No bridge resource found.");
        return result.Data[0].BridgeId;
    }

    private async Task<HueResponse<T>> GetResourceAsync<T>(
        string path, string accessToken, string applicationKey, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ClipV2BaseUrl}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("hue-application-key", applicationKey);

        _logger.LogDebug("Requesting Hue API: {Path}", path);

        using var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Hue API request failed for {Path} with status {StatusCode}: {Body}",
                path, statusCode, errorBody.Length > 500 ? errorBody[..500] : errorBody);

            throw new HttpRequestException(
                $"Hue API request failed for {path} with status {statusCode}",
                null,
                response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<HueResponse<T>>(json, JsonOptions);

        return result ?? throw new InvalidOperationException($"Failed to deserialize Hue API response for {path}.");
    }

    private async Task<HueTokenResponse> PostTokenRequestAsync(Dictionary<string, string> formData, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_hueAppSettings.ClientId}:{_hueAppSettings.ClientSecret}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(formData);

        using var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Token request failed with status {StatusCode}: {Body}",
                (int)response.StatusCode, errorBody.Length > 500 ? errorBody[..500] : errorBody);
            throw new HttpRequestException(
                $"Token request failed with status {(int)response.StatusCode}",
                null,
                response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var tokenResponse = JsonSerializer.Deserialize<HueTokenResponse>(json, JsonOptions);

        return tokenResponse ?? throw new InvalidOperationException("Failed to deserialize token response.");
    }
}
