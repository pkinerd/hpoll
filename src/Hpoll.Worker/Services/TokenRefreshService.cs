namespace Hpoll.Worker.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Hpoll.Core.Interfaces;
using Hpoll.Data;

public class TokenRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TokenRefreshService> _logger;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(24);
    private const int MaxRetries = 3;

    public TokenRefreshService(
        IServiceScopeFactory scopeFactory,
        ILogger<TokenRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Token refresh service started. Interval: {Hours}h", RefreshInterval.TotalHours);

        // Wait a bit on startup to let things initialize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAllTokensAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in token refresh cycle");
            }

            try
            {
                await Task.Delay(RefreshInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RefreshAllTokensAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();
        var hueClient = scope.ServiceProvider.GetRequiredService<IHueApiClient>();

        var hubs = await db.Hubs
            .Where(h => h.Status == "active")
            .ToListAsync(ct);

        _logger.LogInformation("Refreshing tokens for {Count} active hubs", hubs.Count);

        foreach (var hub in hubs)
        {
            var success = false;
            for (int retry = 0; retry < MaxRetries; retry++)
            {
                try
                {
                    var tokenResponse = await hueClient.RefreshTokenAsync(hub.RefreshToken, ct);

                    hub.AccessToken = tokenResponse.AccessToken;
                    if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                    {
                        hub.RefreshToken = tokenResponse.RefreshToken;
                    }
                    hub.TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    hub.UpdatedAt = DateTime.UtcNow;

                    await db.SaveChangesAsync(ct);

                    _logger.LogInformation(
                        "Hub {BridgeId}: token refreshed. Expires at {Expiry}",
                        hub.HueBridgeId, hub.TokenExpiresAt);

                    success = true;
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Hub {BridgeId}: token refresh attempt {Attempt}/{Max} failed",
                        hub.HueBridgeId, retry + 1, MaxRetries);

                    if (retry < MaxRetries - 1)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, retry + 1));
                        await Task.Delay(delay, ct);
                    }
                }
            }

            if (!success)
            {
                _logger.LogError("Hub {BridgeId}: token refresh failed after {Max} retries. Marking as needs_reauth",
                    hub.HueBridgeId, MaxRetries);
                hub.Status = "needs_reauth";
                hub.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
