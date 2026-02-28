using Hpoll.Core.Models;

namespace Hpoll.Core.Interfaces;

public interface IHueApiClient
{
    Task<HueResponse<HueMotionResource>> GetMotionSensorsAsync(string accessToken, string applicationKey, CancellationToken ct = default);
    Task<HueResponse<HueTemperatureResource>> GetTemperatureSensorsAsync(string accessToken, string applicationKey, CancellationToken ct = default);
    Task<HueResponse<HueDeviceResource>> GetDevicesAsync(string accessToken, string applicationKey, CancellationToken ct = default);
    Task<HueResponse<HueDevicePowerResource>> GetDevicePowerAsync(string accessToken, string applicationKey, CancellationToken ct = default);
    Task<HueTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<HueTokenResponse> ExchangeAuthorizationCodeAsync(string code, string redirectUri, CancellationToken ct = default);
    Task EnableLinkButtonAsync(string accessToken, CancellationToken ct = default);
    Task<string> RegisterApplicationAsync(string accessToken, string deviceType = "hpoll", CancellationToken ct = default);
    Task<string> GetBridgeIdAsync(string accessToken, string applicationKey, CancellationToken ct = default);
}
