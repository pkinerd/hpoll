using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Hpoll.Core.Configuration;
using Hpoll.Core.Constants;
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
    private readonly FakeTimeProvider _fakeTime;
    private readonly string _dbName;

    public TokenRefreshServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _mockHueClient = new Mock<IHueApiClient>();
        _fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero));

        var services = new ServiceCollection();
        services.AddDbContext<HpollDbContext>(options =>
            options.UseInMemoryDatabase(_dbName));
        services.AddScoped<IHueApiClient>(_ => _mockHueClient.Object);
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        foreach (var scope in _scopes) scope.Dispose();
        _serviceProvider.Dispose();
    }

    private readonly List<IServiceScope> _scopes = new();

    private HpollDbContext CreateDb()
    {
        var scope = _serviceProvider.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<HpollDbContext>();
    }

    private async Task<Hub> SeedHubAsync(string status = HubStatus.Active, DateTime? tokenExpiresAt = null)
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
            TokenExpiresAt = tokenExpiresAt ?? _fakeTime.GetUtcNow().UtcDateTime.AddDays(1),
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
            Options.Create(settings ?? new PollingSettings()),
            new Mock<ISystemInfoService>().Object,
            _fakeTime);
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
        Assert.Equal(HubStatus.NeedsReauth, updatedHub.Status);
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
        Assert.Equal(HubStatus.Active, updatedHub.Status);
    }

    [Fact]
    public async Task RefreshExpiringTokens_TokenNotNearExpiry_SkipsRefresh()
    {
        // Token expires far in the future (well beyond threshold)
        await SeedHubAsync(tokenExpiresAt: _fakeTime.GetUtcNow().UtcDateTime.AddDays(30));

        var service = CreateService(new PollingSettings { TokenRefreshThresholdHours = 48 });
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        _mockHueClient.Verify(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshExpiringTokens_MultipleHubs_OnlyRefreshesExpiring()
    {
        // Hub1: near expiry (within threshold)
        var hub1 = await SeedHubAsync(tokenExpiresAt: _fakeTime.GetUtcNow().UtcDateTime.AddHours(12));
        // Hub2: far from expiry
        var hub2 = await SeedHubAsync(tokenExpiresAt: _fakeTime.GetUtcNow().UtcDateTime.AddDays(30));

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
        await SeedHubAsync(status: HubStatus.Inactive, tokenExpiresAt: _fakeTime.GetUtcNow().UtcDateTime.AddHours(1));

        var service = CreateService();
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        _mockHueClient.Verify(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshExpiringTokens_UpdatedAtTimestamp_SetOnSuccess()
    {
        var hub = await SeedHubAsync();
        var expectedTime = _fakeTime.GetUtcNow().UtcDateTime;

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
        Assert.Equal(expectedTime, updatedHub.UpdatedAt);
    }

    [Fact]
    public async Task RefreshExpiringTokens_UpdatedAtTimestamp_SetOnNeedsReauth()
    {
        var hub = await SeedHubAsync();
        var expectedTime = _fakeTime.GetUtcNow().UtcDateTime;

        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Fail"));

        var service = CreateService();
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal(HubStatus.NeedsReauth, updatedHub.Status);
        Assert.Equal(expectedTime, updatedHub.UpdatedAt);
    }

    [Fact]
    public async Task ExecuteAsync_StopsGracefullyOnCancellation()
    {
        var service = CreateService(new PollingSettings { TokenRefreshCheckHours = 24 });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await service.StartAsync(CancellationToken.None);
        try { await Task.Delay(500, cts.Token); } catch (OperationCanceledException) { }
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesSystemInfoMetrics()
    {
        var hub = await SeedHubAsync(tokenExpiresAt: _fakeTime.GetUtcNow().UtcDateTime.AddDays(30));
        var systemInfoMock = new Mock<ISystemInfoService>();

        var service = new TokenRefreshService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TokenRefreshService>.Instance,
            Options.Create(new PollingSettings { TokenRefreshThresholdHours = 48, TokenRefreshCheckHours = 24 }),
            systemInfoMock.Object,
            _fakeTime);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await service.StartAsync(cts.Token); await Task.Delay(1000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        systemInfoMock.Verify(s => s.SetBatchAsync("Runtime",
            It.Is<Dictionary<string, string>>(d => d.ContainsKey("runtime.last_token_check") && d.ContainsKey("runtime.next_token_check")),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_SystemInfoFailure_DoesNotCrashService()
    {
        await SeedHubAsync(tokenExpiresAt: _fakeTime.GetUtcNow().UtcDateTime.AddDays(30));
        var systemInfoMock = new Mock<ISystemInfoService>();
        systemInfoMock.Setup(s => s.SetBatchAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("System info write failed"));

        var service = new TokenRefreshService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TokenRefreshService>.Instance,
            Options.Create(new PollingSettings { TokenRefreshCheckHours = 24 }),
            systemInfoMock.Object,
            _fakeTime);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await service.StartAsync(cts.Token); await Task.Delay(1000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Service should still be running — no unhandled exception
    }

    [Fact]
    public async Task RefreshExpiringTokens_NeedsReauthHub_NotIncluded()
    {
        await SeedHubAsync(status: HubStatus.NeedsReauth, tokenExpiresAt: _fakeTime.GetUtcNow().UtcDateTime.AddHours(1));

        var service = CreateService();
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        _mockHueClient.Verify(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshExpiringTokens_NoHubs_CompletesWithoutError()
    {
        var service = CreateService();
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        _mockHueClient.Verify(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshExpiringTokens_AllRetriesFail_HubMarkedNeedsReauthAndAccessTokenUnchanged()
    {
        var hub = await SeedHubAsync(tokenExpiresAt: _fakeTime.GetUtcNow().UtcDateTime.AddHours(1));

        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Server error"));

        var service = CreateService(new PollingSettings { TokenRefreshMaxRetries = 3, TokenRefreshThresholdHours = 48 });
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal(HubStatus.NeedsReauth, updatedHub.Status);
        Assert.Equal("old-access-token", updatedHub.AccessToken); // Token not changed
        _mockHueClient.Verify(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDuringDelay_StopsGracefully()
    {
        var service = CreateService(new PollingSettings { TokenRefreshCheckHours = 100 });

        // Start, let it run once and enter the delay, then cancel
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await service.StartAsync(CancellationToken.None);
        try { await Task.Delay(1000, cts.Token); } catch (OperationCanceledException) { }
        await service.StopAsync(CancellationToken.None);

        // No exception means it stopped gracefully via the OperationCanceledException catch in the delay
    }

    [Fact]
    public async Task RefreshExpiringTokens_TokenExpiresAtBoundary_RefreshesToken()
    {
        // Token expires exactly at the threshold boundary
        var settings = new PollingSettings { TokenRefreshThresholdHours = 48 };
        await SeedHubAsync(tokenExpiresAt: _fakeTime.GetUtcNow().UtcDateTime.AddHours(48));

        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueTokenResponse
            {
                AccessToken = "boundary-token",
                RefreshToken = "boundary-refresh",
                TokenType = "Bearer",
                ExpiresIn = 604800
            });

        var service = CreateService(settings);
        await service.RefreshExpiringTokensAsync(CancellationToken.None);

        // At exactly the threshold, time always advances between seed and check,
        // so timeUntilExpiry < threshold and the token is always refreshed
        _mockHueClient.Verify(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
