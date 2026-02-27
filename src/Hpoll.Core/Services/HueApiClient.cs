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
    private const string TokenUrl = "https://api.meethue.com/v2/oauth2/token";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HpollSettings _settings;
    private readonly ILogger<HueApiClient> _logger;

    public HueApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<HpollSettings> settings,
        ILogger<HueApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
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

    public async Task<HueTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient();

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_settings.HueApp.ClientId}:{_settings.HueApp.ClientSecret}"));

        var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Token refresh failed with status {(int)response.StatusCode}: {body}",
                null,
                response.StatusCode);
        }

        var tokenResponse = await JsonSerializer.DeserializeAsync<HueTokenResponse>(
            await response.Content.ReadAsStreamAsync(ct), JsonOptions, ct);

        return tokenResponse ?? throw new InvalidOperationException("Failed to deserialize token response.");
    }

    private async Task<HueResponse<T>> GetResourceAsync<T>(
        string path, string accessToken, string applicationKey, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, $"{ClipV2BaseUrl}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("hue-application-key", applicationKey);

        _logger.LogDebug("Requesting Hue API: {Path}", path);

        var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var statusCode = (int)response.StatusCode;

            if (statusCode is 429 or 503)
            {
                throw new HttpRequestException(
                    $"Hue API returned {statusCode} for {path}: {body}",
                    null,
                    response.StatusCode);
            }

            throw new HttpRequestException(
                $"Hue API request failed for {path} with status {statusCode}: {body}",
                null,
                response.StatusCode);
        }

        var result = await JsonSerializer.DeserializeAsync<HueResponse<T>>(
            await response.Content.ReadAsStreamAsync(ct), JsonOptions, ct);

        return result ?? throw new InvalidOperationException($"Failed to deserialize Hue API response for {path}.");
    }
}
