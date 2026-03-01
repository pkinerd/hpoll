using Hpoll.Core.Models;

namespace Hpoll.Core.Interfaces;

/// <summary>
/// Client for the Philips Hue Remote API (CLIP v2) and OAuth token endpoints.
/// All sensor/device methods require both an OAuth access token and a
/// hue-application-key obtained during hub registration.
/// </summary>
public interface IHueApiClient
{
    /// <summary>Retrieves all motion sensor resources from the bridge.</summary>
    Task<HueResponse<HueMotionResource>> GetMotionSensorsAsync(string accessToken, string applicationKey, CancellationToken ct = default);

    /// <summary>Retrieves all temperature sensor resources from the bridge.</summary>
    Task<HueResponse<HueTemperatureResource>> GetTemperatureSensorsAsync(string accessToken, string applicationKey, CancellationToken ct = default);

    /// <summary>Retrieves all device resources (sensors, lights, etc.) from the bridge.</summary>
    Task<HueResponse<HueDeviceResource>> GetDevicesAsync(string accessToken, string applicationKey, CancellationToken ct = default);

    /// <summary>Retrieves battery/power state for all devices that report it.</summary>
    Task<HueResponse<HueDevicePowerResource>> GetDevicePowerAsync(string accessToken, string applicationKey, CancellationToken ct = default);

    /// <summary>Exchanges a refresh token for a new access/refresh token pair.</summary>
    Task<HueTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Exchanges an OAuth authorization code for an initial token pair during hub registration.</summary>
    Task<HueTokenResponse> ExchangeAuthorizationCodeAsync(string code, string redirectUri, CancellationToken ct = default);

    /// <summary>Activates the bridge link button remotely, required before registering an application key.</summary>
    Task EnableLinkButtonAsync(string accessToken, CancellationToken ct = default);

    /// <summary>Registers a new application key (hue-application-key) with the bridge.</summary>
    Task<string> RegisterApplicationAsync(string accessToken, string deviceType = "hpoll", CancellationToken ct = default);

    /// <summary>Discovers the bridge's unique identifier via the bridge resource endpoint.</summary>
    Task<string> GetBridgeIdAsync(string accessToken, string applicationKey, CancellationToken ct = default);
}
