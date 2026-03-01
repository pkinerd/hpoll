using Hpoll.Core.Models;

namespace Hpoll.Data.Entities;

public static class HubExtensions
{
    public static void ApplyTokenResponse(this Hub hub, HueTokenResponse response, DateTime utcNow)
    {
        hub.AccessToken = response.AccessToken;
        if (!string.IsNullOrEmpty(response.RefreshToken))
            hub.RefreshToken = response.RefreshToken;
        hub.TokenExpiresAt = utcNow.AddSeconds(response.ExpiresIn);
        hub.UpdatedAt = utcNow;
    }
}
