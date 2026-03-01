using Hpoll.Core.Models;
using Hpoll.Data.Entities;

namespace Hpoll.Core.Tests;

public class HubExtensionsTests
{
    [Fact]
    public void ApplyTokenResponse_SetsAccessToken()
    {
        var hub = new Hub { AccessToken = "old" };
        var response = new HueTokenResponse { AccessToken = "new-access", RefreshToken = "new-refresh", ExpiresIn = 3600 };

        hub.ApplyTokenResponse(response, new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal("new-access", hub.AccessToken);
    }

    [Fact]
    public void ApplyTokenResponse_SetsRefreshToken_WhenNotEmpty()
    {
        var hub = new Hub { RefreshToken = "old-refresh" };
        var response = new HueTokenResponse { AccessToken = "access", RefreshToken = "new-refresh", ExpiresIn = 3600 };

        hub.ApplyTokenResponse(response, new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal("new-refresh", hub.RefreshToken);
    }

    [Fact]
    public void ApplyTokenResponse_PreservesRefreshToken_WhenEmpty()
    {
        var hub = new Hub { RefreshToken = "existing-refresh" };
        var response = new HueTokenResponse { AccessToken = "access", RefreshToken = "", ExpiresIn = 3600 };

        hub.ApplyTokenResponse(response, new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal("existing-refresh", hub.RefreshToken);
    }

    [Fact]
    public void ApplyTokenResponse_PreservesRefreshToken_WhenNull()
    {
        var hub = new Hub { RefreshToken = "existing-refresh" };
        var response = new HueTokenResponse { AccessToken = "access", RefreshToken = null!, ExpiresIn = 3600 };

        hub.ApplyTokenResponse(response, new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal("existing-refresh", hub.RefreshToken);
    }

    [Fact]
    public void ApplyTokenResponse_SetsTokenExpiresAt()
    {
        var hub = new Hub();
        var response = new HueTokenResponse { AccessToken = "access", RefreshToken = "refresh", ExpiresIn = 7200 };
        var utcNow = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

        hub.ApplyTokenResponse(response, utcNow);

        Assert.Equal(utcNow.AddSeconds(7200), hub.TokenExpiresAt);
    }

    [Fact]
    public void ApplyTokenResponse_SetsUpdatedAt()
    {
        var hub = new Hub();
        var response = new HueTokenResponse { AccessToken = "access", RefreshToken = "refresh", ExpiresIn = 3600 };
        var utcNow = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

        hub.ApplyTokenResponse(response, utcNow);

        Assert.Equal(utcNow, hub.UpdatedAt);
    }
}
