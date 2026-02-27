using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
    private readonly IConfiguration _configuration;
    private readonly string _dbName;
    private readonly string _tempDataPath;

    public TokenRefreshServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _mockHueClient = new Mock<IHueApiClient>();
        _tempDataPath = Path.Combine(Path.GetTempPath(), $"hpoll-test-{_dbName}");
        Directory.CreateDirectory(_tempDataPath);
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "DataPath", _tempDataPath } })
            .Build();

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
        if (Directory.Exists(_tempDataPath))
            Directory.Delete(_tempDataPath, recursive: true);
    }

    private HpollDbContext CreateDb()
    {
        var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<HpollDbContext>();
    }

    private async Task<Hub> SeedHubAsync()
    {
        using var db = CreateDb();
        var customer = new Customer { Name = "Test", Email = "test@example.com" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "001788FFFE123456",
            HueApplicationKey = "appkey",
            AccessToken = "old-access-token",
            RefreshToken = "old-refresh-token",
            TokenExpiresAt = DateTime.UtcNow.AddDays(1),
            Status = "active"
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();
        return hub;
    }

    private async Task InvokeRefreshExpiringTokensAsync(TokenRefreshService service, CancellationToken ct)
    {
        var method = typeof(TokenRefreshService).GetMethod("RefreshExpiringTokensAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task)method!.Invoke(service, new object[] { ct })!;
        await task;
    }

    [Fact]
    public async Task RefreshAllTokens_UpdatesAccessToken()
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

        var service = new TokenRefreshService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TokenRefreshService>.Instance,
            _configuration);

        await InvokeRefreshExpiringTokensAsync(service, CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal("new-access-token", updatedHub.AccessToken);
        Assert.Equal("new-refresh-token", updatedHub.RefreshToken);
    }

    [Fact]
    public async Task RefreshAllTokens_OnFailure_MarksHubAsNeedsReauth()
    {
        var hub = await SeedHubAsync();

        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unauthorized"));

        var service = new TokenRefreshService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TokenRefreshService>.Instance,
            _configuration);

        await InvokeRefreshExpiringTokensAsync(service, CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal("needs_reauth", updatedHub.Status);
    }

    [Fact]
    public async Task RefreshAllTokens_RetriesOnFailure()
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

        var service = new TokenRefreshService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TokenRefreshService>.Instance,
            _configuration);

        await InvokeRefreshExpiringTokensAsync(service, CancellationToken.None);

        Assert.Equal(3, callCount);
        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal("recovered-token", updatedHub.AccessToken);
        Assert.Equal("active", updatedHub.Status);
    }
}
