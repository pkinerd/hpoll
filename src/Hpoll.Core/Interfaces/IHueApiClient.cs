using Hpoll.Core.Models;

namespace Hpoll.Core.Interfaces;

public interface IHueApiClient
{
    Task<HueResponse<HueMotionResource>> GetMotionSensorsAsync(string accessToken, string applicationKey, CancellationToken ct = default);
    Task<HueResponse<HueTemperatureResource>> GetTemperatureSensorsAsync(string accessToken, string applicationKey, CancellationToken ct = default);
    Task<HueResponse<HueDeviceResource>> GetDevicesAsync(string accessToken, string applicationKey, CancellationToken ct = default);
    Task<HueTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
}
