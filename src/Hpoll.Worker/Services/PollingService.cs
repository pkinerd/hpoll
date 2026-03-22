namespace Hpoll.Worker.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Constants;
using Hpoll.Core.Interfaces;
using Hpoll.Core.Models;
using Hpoll.Data;
using Hpoll.Data.Entities;
using System.Text.Json;

/// <summary>
/// Periodically polls all active Hue Bridge hubs for motion, temperature, and battery
/// sensor data, stores readings in the database, and cleans up old data beyond the
/// configured retention window.
/// </summary>
public class PollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PollingService> _logger;
    private readonly PollingSettings _settings;
    private readonly ISystemInfoService _systemInfo;
    private readonly TimeProvider _timeProvider;
    private bool _firstCycle = true;
    private int _totalPollCycles;

    public PollingService(
        IServiceScopeFactory scopeFactory,
        ILogger<PollingService> logger,
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
        _logger.LogInformation("Polling service started. Interval: {Interval} minutes", _settings.IntervalMinutes);

        // Run immediately on startup, then on timer
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAllHubsAsync(_firstCycle, stoppingToken);
                _firstCycle = false;
                _totalPollCycles++;
                await CleanupOldDataAsync(stoppingToken);

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                await _systemInfo.TrySetBatchAsync("Runtime", new Dictionary<string, string>
                {
                    ["runtime.last_poll_completed"] = now.ToString("O"),
                    ["runtime.next_poll_due"] = now.AddMinutes(_settings.IntervalMinutes).ToString("O"),
                    ["runtime.total_poll_cycles"] = _totalPollCycles.ToString()
                }, _logger, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in polling cycle");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_settings.IntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task PollAllHubsAsync(bool forceBatteryPoll, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();
        var hueClient = scope.ServiceProvider.GetRequiredService<IHueApiClient>();

        var activeHubs = await db.Hubs
            .Include(h => h.Devices)
            .Where(h => h.Status == HubStatus.Active)
            .ToListAsync(ct);

        _logger.LogInformation("Polling {Count} active hubs", activeHubs.Count);

        foreach (var hub in activeHubs)
        {
            if (hub.TokenExpiresAt <= _timeProvider.GetUtcNow().UtcDateTime)
            {
                _logger.LogWarning("Hub {BridgeId}: token expired, skipping poll", hub.HueBridgeId);
                continue;
            }
            await PollHubAsync(db, hueClient, hub, forceBatteryPoll, ct);
        }
    }

    private async Task PollHubAsync(HpollDbContext db, IHueApiClient hueClient, Hub hub, bool forceBatteryPoll, CancellationToken ct)
    {
        var pollTime = _timeProvider.GetUtcNow().UtcDateTime;
        var log = new PollingLog { HubId = hub.Id, Timestamp = pollTime };
        int apiCalls = 0;

        try
        {
            // Determine if battery data should be fetched this cycle
            var shouldPollBattery = forceBatteryPoll
                || !hub.LastBatteryPollUtc.HasValue
                || (pollTime - hub.LastBatteryPollUtc.Value).TotalHours >= _settings.BatteryPollIntervalHours;

            // Fetch all sensor data in parallel
            var motionTask = hueClient.GetMotionSensorsAsync(hub.AccessToken, hub.HueApplicationKey, ct);
            var tempTask = hueClient.GetTemperatureSensorsAsync(hub.AccessToken, hub.HueApplicationKey, ct);
            var deviceTask = hueClient.GetDevicesAsync(hub.AccessToken, hub.HueApplicationKey, ct);
            var batteryTask = shouldPollBattery
                ? hueClient.GetDevicePowerAsync(hub.AccessToken, hub.HueApplicationKey, ct)
                : Task.FromResult(new HueResponse<HueDevicePowerResource>());

            await Task.WhenAll(motionTask, tempTask, deviceTask, batteryTask);
            apiCalls = shouldPollBattery ? 4 : 3;

            var motionResponse = await motionTask;
            var tempResponse = await tempTask;
            var deviceResponse = await deviceTask;
            var batteryResponse = await batteryTask;

            // Build device lookup: device ID -> device (for owner.rid lookups)
            var deviceById = deviceResponse.Data.ToDictionary(d => d.Id, d => d);

            // Compute the cutoff for motion detection: the lower of last poll time
            // or current time minus the polling interval. If Changed is after this
            // cutoff, motion occurred since we last checked.
            // The Hue API motion boolean is momentary and resets quickly, so with
            // 60-minute polling we'd miss most events if we relied on it directly.
            var intervalCutoff = pollTime.AddMinutes(-_settings.IntervalMinutes);
            var motionCutoff = hub.LastPolledAt.HasValue
                ? new DateTime(Math.Min(hub.LastPolledAt.Value.Ticks, intervalCutoff.Ticks), DateTimeKind.Utc)
                : intervalCutoff;

            // Process motion readings
            foreach (var motion in motionResponse.Data)
            {
                if (!motion.Enabled || motion.Motion.MotionReport == null) continue;

                // owner.rid is the parent device ID
                var deviceName = deviceById.TryGetValue(motion.Owner.Rid, out var ownerDevice)
                    ? ownerDevice.Metadata.Name
                    : "Unknown";

                var dbDevice = await GetOrCreateDeviceAsync(db, hub, motion.Owner.Rid, DeviceTypes.MotionSensor, deviceName, ct);

                var motionDetected = motion.Motion.MotionReport.Changed > motionCutoff;

                db.DeviceReadings.Add(new DeviceReading
                {
                    DeviceId = dbDevice.Id,
                    Timestamp = pollTime,
                    ReadingType = ReadingTypes.Motion,
                    Value = JsonSerializer.Serialize(new
                    {
                        motion = motionDetected,
                        changed = motion.Motion.MotionReport.Changed
                    })
                });
            }

            // Process temperature readings
            foreach (var temp in tempResponse.Data)
            {
                if (!temp.Enabled || temp.Temperature.TemperatureReport == null) continue;

                // owner.rid is the parent device ID
                var deviceName = deviceById.TryGetValue(temp.Owner.Rid, out var ownerDevice)
                    ? ownerDevice.Metadata.Name
                    : "Unknown";

                var dbDevice = await GetOrCreateDeviceAsync(db, hub, temp.Owner.Rid, DeviceTypes.TemperatureSensor, deviceName, ct);

                db.DeviceReadings.Add(new DeviceReading
                {
                    DeviceId = dbDevice.Id,
                    Timestamp = pollTime,
                    ReadingType = ReadingTypes.Temperature,
                    Value = JsonSerializer.Serialize(new
                    {
                        temperature = temp.Temperature.TemperatureReport.Temperature,
                        changed = temp.Temperature.TemperatureReport.Changed
                    })
                });
            }

            // Process battery readings (only when polled this cycle)
            if (shouldPollBattery && batteryResponse.Data.Count > 0)
            {
                // Track which device IDs are still present on the hub so we can
                // remove stale battery devices that have been retired/removed.
                var currentBatteryDeviceIds = new HashSet<string>();

                foreach (var power in batteryResponse.Data)
                {
                    if (power.PowerState.BatteryLevel == null) continue;

                    currentBatteryDeviceIds.Add(power.Owner.Rid);

                    var deviceName = deviceById.TryGetValue(power.Owner.Rid, out var ownerDevice)
                        ? ownerDevice.Metadata.Name
                        : "Unknown";

                    var dbDevice = await GetOrCreateDeviceAsync(db, hub, power.Owner.Rid, DeviceTypes.Battery, deviceName, ct);

                    db.DeviceReadings.Add(new DeviceReading
                    {
                        DeviceId = dbDevice.Id,
                        Timestamp = pollTime,
                        ReadingType = ReadingTypes.Battery,
                        Value = JsonSerializer.Serialize(new
                        {
                            battery_level = power.PowerState.BatteryLevel.Value,
                            battery_state = power.PowerState.BatteryState ?? "unknown"
                        })
                    });
                }

                // Remove battery devices that are no longer present on the hub
                // (e.g. retired/removed devices whose old readings would still appear in emails).
                var staleDevices = await db.Devices
                    .Where(d => d.HubId == hub.Id && d.DeviceType == DeviceTypes.Battery && !currentBatteryDeviceIds.Contains(d.HueDeviceId))
                    .ToListAsync(ct);

                if (staleDevices.Count > 0)
                {
                    var staleDeviceIds = staleDevices.Select(d => d.Id).ToList();
                    var staleReadings = await db.DeviceReadings
                        .Where(r => staleDeviceIds.Contains(r.DeviceId))
                        .ToListAsync(ct);
                    db.DeviceReadings.RemoveRange(staleReadings);
                    db.Devices.RemoveRange(staleDevices);
                    _logger.LogInformation(
                        "Hub {BridgeId}: removed {Count} stale battery device(s) no longer on hub",
                        hub.HueBridgeId, staleDevices.Count);
                }

                hub.LastBatteryPollUtc = pollTime;

                _logger.LogInformation(
                    "Hub {BridgeId}: battery data fetched. {BatteryCount} device_power resources",
                    hub.HueBridgeId, batteryResponse.Data.Count);
            }

            hub.LastSuccessAt = pollTime;
            hub.ConsecutiveFailures = 0;
            log.Success = true;

            _logger.LogInformation(
                "Hub {BridgeId}: polled successfully. {MotionCount} motion, {TempCount} temperature readings",
                hub.HueBridgeId, motionResponse.Data.Count, tempResponse.Data.Count);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Hub {BridgeId}: unauthorized (401). Attempting immediate token refresh", hub.HueBridgeId);
            hub.ConsecutiveFailures++;
            log.Success = false;

            try
            {
                var tokenResponse = await hueClient.RefreshTokenAsync(hub.RefreshToken, ct);
                hub.ApplyTokenResponse(tokenResponse, _timeProvider.GetUtcNow().UtcDateTime);
                log.ErrorMessage = "Unauthorized (401) - token refreshed successfully, will retry next cycle";
                _logger.LogInformation("Hub {BridgeId}: token refreshed after 401. Expires at {Expiry}", hub.HueBridgeId, hub.TokenExpiresAt);
            }
            catch (Exception refreshEx)
            {
                _logger.LogError(refreshEx, "Hub {BridgeId}: token refresh failed after 401. Marking as needs_reauth", hub.HueBridgeId);
                hub.Status = HubStatus.NeedsReauth;
                log.ErrorMessage = "Unauthorized (401) - token refresh failed, hub marked as needs_reauth";
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning("Hub {BridgeId}: rate limited (429)", hub.HueBridgeId);
            hub.ConsecutiveFailures++;
            log.Success = false;
            log.ErrorMessage = "Rate limited (429)";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogWarning("Hub {BridgeId}: bridge offline (503)", hub.HueBridgeId);
            hub.ConsecutiveFailures++;
            log.Success = false;
            log.ErrorMessage = "Bridge offline (503)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hub {BridgeId}: polling failed", hub.HueBridgeId);
            hub.ConsecutiveFailures++;
            log.Success = false;
            log.ErrorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
        }
        finally
        {
            try
            {
                hub.LastPolledAt = pollTime;
                hub.UpdatedAt = pollTime;
                log.ApiCallsMade = apiCalls;
                db.PollingLogs.Add(log);
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hub {BridgeId}: failed to save polling log", hub.HueBridgeId);
            }
        }
    }

    internal async Task CleanupOldDataAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();
            var cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-_settings.DataRetentionHours);

            var totalReadings = await db.DeviceReadings
                .Where(r => r.Timestamp < cutoff)
                .ExecuteDeleteAsync(ct);

            var totalLogs = await db.PollingLogs
                .Where(l => l.Timestamp < cutoff)
                .ExecuteDeleteAsync(ct);

            if (totalReadings > 0 || totalLogs > 0)
            {
                _logger.LogInformation(
                    "Data retention cleanup: deleted {Readings} readings and {Logs} polling logs older than {Hours} hours",
                    totalReadings, totalLogs, _settings.DataRetentionHours);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Data retention cleanup failed");
        }
    }

    private async Task<Device> GetOrCreateDeviceAsync(
        HpollDbContext db, Hub hub, string hueDeviceId, string deviceType, string name, CancellationToken ct)
    {
        var device = hub.Devices.FirstOrDefault(d => d.HueDeviceId == hueDeviceId);
        if (device == null)
        {
            device = new Device
            {
                HubId = hub.Id,
                HueDeviceId = hueDeviceId,
                DeviceType = deviceType,
                Name = name,
            };
            db.Devices.Add(device);
            await db.SaveChangesAsync(ct);
            hub.Devices.Add(device);
        }
        else if (device.Name != name)
        {
            device.Name = name;
            device.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }
        return device;
    }
}
