namespace Hpoll.Worker.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Hpoll.Core.Interfaces;
using Hpoll.Data;

public class TokenRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TokenRefreshService> _logger;
    private readonly string _tokenLogPath;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromHours(48);
    private const int MaxRetries = 3;

    public TokenRefreshService(
        IServiceScopeFactory scopeFactory,
        ILogger<TokenRefreshService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var dataPath = configuration.GetValue<string>("DataPath") ?? "data";
        _tokenLogPath = Path.Combine(dataPath, "token-refresh.log");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Token refresh service started. Check interval: {Hours}h, refresh threshold: {Threshold}h before expiry",
            CheckInterval.TotalHours, RefreshThreshold.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshExpiringTokensAsync(stoppingToken);
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
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RefreshExpiringTokensAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();
        var hueClient = scope.ServiceProvider.GetRequiredService<IHueApiClient>();

        var hubs = await db.Hubs
            .Where(h => h.Status == "active")
            .ToListAsync(ct);

        _logger.LogInformation("Checking tokens for {Count} active hubs", hubs.Count);

        foreach (var hub in hubs)
        {
            var timeUntilExpiry = hub.TokenExpiresAt - DateTime.UtcNow;
            if (timeUntilExpiry > RefreshThreshold)
            {
                _logger.LogDebug(
                    "Hub {BridgeId}: token still valid for {Hours:F0}h, skipping refresh",
                    hub.HueBridgeId, timeUntilExpiry.TotalHours);
                continue;
            }

            _logger.LogInformation(
                "Hub {BridgeId}: token expires in {Hours:F1}h (threshold: {Threshold}h), refreshing",
                hub.HueBridgeId, timeUntilExpiry.TotalHours, RefreshThreshold.TotalHours);

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

                    await AppendTokenLogAsync(hub.HueBridgeId, hub.AccessToken, hub.RefreshToken);

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

    private async Task AppendTokenLogAsync(string bridgeId, string accessToken, string refreshToken)
    {
        try
        {
            var line = $"{DateTime.UtcNow:O} bridge={bridgeId} access_token={accessToken} refresh_token={refreshToken}\n";
            await File.AppendAllTextAsync(_tokenLogPath, line);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write token refresh log entry");
        }
    }
}
