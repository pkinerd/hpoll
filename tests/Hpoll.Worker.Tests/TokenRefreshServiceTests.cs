using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Core.Models;
using Hpoll.Data;
using Hpoll.Data.Entities;
using Hpoll.Worker.Services;

namespace Hpoll.Worker.Tests;

public class TokenRefreshServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IHueApiClient> _mockHueClient;
    private readonly string _dbName;

    public TokenRefreshServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _mockHueClient = new Mock<IHueApiClient>();

        var services = new ServiceCollection();
        services.AddDbContext<HpollDbContext>(options =>
            options.UseInMemoryDatabase(_dbName));
        services.AddScoped<IHueApiClient>(_ => _mockHueClient.Object);
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    private HpollDbContext CreateDb()
    {
        var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<HpollDbContext>();
    }

    private async Task<Hub> SeedHubAsync(string status = "active", DateTime? tokenExpiresAt = null)
    {
        using var db = CreateDb();
        var customer = new Customer { Name = "Test", Email = $"test-{Guid.NewGuid()}@example.com" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = $"001788FFFE{Guid.NewGuid().ToString()[..6]}",
            HueApplicationKey = "appkey",
            AccessToken = "old-access-token",
            RefreshToken = "old-refresh-token",
            TokenExpiresAt = tokenExpiresAt ?? DateTime.UtcNow.AddDays(1),
            Status = status
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();
        return hub;
    }

    private TokenRefreshService CreateService(PollingSettings? settings = null)
    {
        return new TokenRefreshService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TokenRefreshService>.Instance,
            Options.Create(settings ?? new PollingSettings()));
    }

    [Fact]
    public async Task RefreshExpiringTokens_UpdatesAccessToken()
    {
        var hub = await SeedHubAsync();

        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueTokenResponse
            {
                AccessToken = "new-access-token",
                RefreshToken = "new-refresh-token",
                TokenType = "Bearer",
                ExpiresIn = 604800
            });

        var service = CreateService();
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal("new-access-token", updatedHub.AccessToken);
        Assert.Equal("new-refresh-token", updatedHub.RefreshToken);
    }

    [Fact]
    public async Task RefreshExpiringTokens_OnFailure_MarksHubAsNeedsReauth()
    {
        var hub = await SeedHubAsync();

        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unauthorized"));

        var service = CreateService();
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal("needs_reauth", updatedHub.Status);
    }

    [Fact]
    public async Task RefreshExpiringTokens_RetriesOnFailure()
    {
        var hub = await SeedHubAsync();

        var callCount = 0;
        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((token, ct) =>
            {
                callCount++;
                if (callCount < 3)
                    throw new HttpRequestException("Temporary failure");
                return Task.FromResult(new HueTokenResponse
                {
                    AccessToken = "recovered-token",
                    RefreshToken = "recovered-refresh",
                    TokenType = "Bearer",
                    ExpiresIn = 604800
                });
            });

        var service = CreateService();
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        Assert.Equal(3, callCount);
        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal("recovered-token", updatedHub.AccessToken);
        Assert.Equal("active", updatedHub.Status);
    }

    [Fact]
    public async Task RefreshExpiringTokens_TokenNotNearExpiry_SkipsRefresh()
    {
        // Token expires far in the future (well beyond threshold)
        await SeedHubAsync(tokenExpiresAt: DateTime.UtcNow.AddDays(30));

        var service = CreateService(new PollingSettings { TokenRefreshThresholdHours = 48 });
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        _mockHueClient.Verify(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshExpiringTokens_MultipleHubs_OnlyRefreshesExpiring()
    {
        // Hub1: near expiry (within threshold)
        var hub1 = await SeedHubAsync(tokenExpiresAt: DateTime.UtcNow.AddHours(12));
        // Hub2: far from expiry
        var hub2 = await SeedHubAsync(tokenExpiresAt: DateTime.UtcNow.AddDays(30));

        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueTokenResponse
            {
                AccessToken = "refreshed",
                RefreshToken = "refreshed-refresh",
                TokenType = "Bearer",
                ExpiresIn = 604800
            });

        var service = CreateService(new PollingSettings { TokenRefreshThresholdHours = 48 });
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        // Only hub1 should have been refreshed
        _mockHueClient.Verify(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        using var db = CreateDb();
        var updatedHub1 = await db.Hubs.FirstAsync(h => h.Id == hub1.Id);
        var updatedHub2 = await db.Hubs.FirstAsync(h => h.Id == hub2.Id);
        Assert.Equal("refreshed", updatedHub1.AccessToken);
        Assert.Equal("old-access-token", updatedHub2.AccessToken);
    }

    [Fact]
    public async Task RefreshExpiringTokens_EmptyRefreshToken_KeepsExistingRefreshToken()
    {
        var hub = await SeedHubAsync();

        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueTokenResponse
            {
                AccessToken = "new-access",
                RefreshToken = "",  // Empty refresh token
                TokenType = "Bearer",
                ExpiresIn = 604800
            });

        var service = CreateService();
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal("new-access", updatedHub.AccessToken);
        Assert.Equal("old-refresh-token", updatedHub.RefreshToken);  // Original kept
    }

    [Fact]
    public async Task RefreshExpiringTokens_VerifyRetryCount_ExactlyMaxRetries()
    {
        await SeedHubAsync();

        var callCount = 0;
        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((_, _) =>
            {
                callCount++;
                throw new HttpRequestException("Always fails");
            });

        var settings = new PollingSettings { TokenRefreshMaxRetries = 5 };
        var service = CreateService(settings);
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        Assert.Equal(5, callCount);
    }

    [Fact]
    public async Task RefreshExpiringTokens_InactiveHub_NotIncluded()
    {
        await SeedHubAsync(status: "inactive", tokenExpiresAt: DateTime.UtcNow.AddHours(1));

        var service = CreateService();
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        _mockHueClient.Verify(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshExpiringTokens_UpdatedAtTimestamp_SetOnSuccess()
    {
        var hub = await SeedHubAsync();
        var beforeRefresh = DateTime.UtcNow;

        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueTokenResponse
            {
                AccessToken = "new-token",
                RefreshToken = "new-refresh",
                TokenType = "Bearer",
                ExpiresIn = 604800
            });

        var service = CreateService();
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.True(updatedHub.UpdatedAt >= beforeRefresh);
    }

    [Fact]
    public async Task RefreshExpiringTokens_UpdatedAtTimestamp_SetOnNeedsReauth()
    {
        var hub = await SeedHubAsync();
        var beforeRefresh = DateTime.UtcNow;

        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Fail"));

        var service = CreateService();
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal("needs_reauth", updatedHub.Status);
        Assert.True(updatedHub.UpdatedAt >= beforeRefresh);
    }
}
