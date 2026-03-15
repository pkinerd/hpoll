using Hpoll.Core.Models;

namespace Hpoll.Data.Entities;

public static class HubExtensions
{
    /// <summary>
    /// Applies an OAuth token response to the hub, updating access token, expiry, and
    /// <see cref="Hub.UpdatedAt"/>. The refresh token is only overwritten when the response
    /// contains a non-empty value, preserving the existing token during grant types that
    /// do not issue a new one.
    /// </summary>
    public static void ApplyTokenResponse(this Hub hub, HueTokenResponse response, DateTime utcNow)
    {
        hub.AccessToken = response.AccessToken;
        if (!string.IsNullOrEmpty(response.RefreshToken))
            hub.RefreshToken = response.RefreshToken;
        hub.TokenExpiresAt = utcNow.AddSeconds(response.ExpiresIn);
        hub.UpdatedAt = utcNow;
    }
}
