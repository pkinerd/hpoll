namespace Hpoll.Worker.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Data;

public class TokenRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TokenRefreshService> _logger;
    private readonly PollingSettings _settings;
    private readonly ISystemInfoService _systemInfo;
    private readonly TimeProvider _timeProvider;

    public TokenRefreshService(
        IServiceScopeFactory scopeFactory,
        ILogger<TokenRefreshService> logger,
        IOptions<PollingSettings> settings,
        ISystemInfoService systemInfo,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;
        _systemInfo = systemInfo;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var checkInterval = TimeSpan.FromHours(_settings.TokenRefreshCheckHours);
        var refreshThreshold = TimeSpan.FromHours(_settings.TokenRefreshThresholdHours);

        _logger.LogInformation(
            "Token refresh service started. Check interval: {Hours}h, refresh threshold: {Threshold}h before expiry",
            checkInterval.TotalHours, refreshThreshold.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshExpiringTokensAsync(stoppingToken);

                try
                {
                    var now = _timeProvider.GetUtcNow().UtcDateTime;
                    await _systemInfo.SetAsync("Runtime", "runtime.last_token_check", now.ToString("O"));
                    await _systemInfo.SetAsync("Runtime", "runtime.next_token_check",
                        now.Add(checkInterval).ToString("O"));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update system info metrics");
                }
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
                await Task.Delay(checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task RefreshExpiringTokensAsync(CancellationToken ct)
    {
        var refreshThreshold = TimeSpan.FromHours(_settings.TokenRefreshThresholdHours);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();
        var hueClient = scope.ServiceProvider.GetRequiredService<IHueApiClient>();

        var hubs = await db.Hubs
            .Where(h => h.Status == "active")
            .ToListAsync(ct);

        _logger.LogInformation("Checking tokens for {Count} active hubs", hubs.Count);

        foreach (var hub in hubs)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var timeUntilExpiry = hub.TokenExpiresAt - now;
            if (timeUntilExpiry > refreshThreshold)
            {
                _logger.LogDebug(
                    "Hub {BridgeId}: token still valid for {Hours:F0}h, skipping refresh",
                    hub.HueBridgeId, timeUntilExpiry.TotalHours);
                continue;
            }

            _logger.LogInformation(
                "Hub {BridgeId}: token expires in {Hours:F1}h (threshold: {Threshold}h), refreshing",
                hub.HueBridgeId, timeUntilExpiry.TotalHours, refreshThreshold.TotalHours);

            var success = false;
            for (int retry = 0; retry < _settings.TokenRefreshMaxRetries; retry++)
            {
                try
                {
                    var tokenResponse = await hueClient.RefreshTokenAsync(hub.RefreshToken, ct);

                    hub.AccessToken = tokenResponse.AccessToken;
                    if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                    {
                        hub.RefreshToken = tokenResponse.RefreshToken;
                    }
                    hub.TokenExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddSeconds(tokenResponse.ExpiresIn);
                    hub.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;

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
                        hub.HueBridgeId, retry + 1, _settings.TokenRefreshMaxRetries);

                    if (retry < _settings.TokenRefreshMaxRetries - 1)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, retry + 1));
                        await Task.Delay(delay, ct);
                    }
                }
            }

            if (!success)
            {
                _logger.LogError("Hub {BridgeId}: token refresh failed after {Max} retries. Marking as needs_reauth",
                    hub.HueBridgeId, _settings.TokenRefreshMaxRetries);
                hub.Status = "needs_reauth";
                hub.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
