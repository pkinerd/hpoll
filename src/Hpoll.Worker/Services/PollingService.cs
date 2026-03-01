namespace Hpoll.Worker.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Core.Models;
using Hpoll.Data;
using Hpoll.Data.Entities;
using System.Text.Json;

public class PollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PollingService> _logger;
    private readonly PollingSettings _settings;
    private readonly TimeProvider _timeProvider;
    private bool _firstCycle = true;

    public PollingService(
        IServiceScopeFactory scopeFactory,
        ILogger<PollingService> logger,
        IOptions<PollingSettings> settings,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;
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
                await CleanupOldDataAsync(stoppingToken);
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
            .Where(h => h.Status == "active")
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
                if (motion.Motion.MotionReport == null) continue;

                // owner.rid is the parent device ID
                var deviceName = deviceById.TryGetValue(motion.Owner.Rid, out var ownerDevice)
                    ? ownerDevice.Metadata.Name
                    : "Unknown";

                var dbDevice = await GetOrCreateDeviceAsync(db, hub, motion.Owner.Rid, "motion_sensor", deviceName, ct);

                var motionDetected = motion.Motion.MotionReport.Changed > motionCutoff;

                db.DeviceReadings.Add(new DeviceReading
                {
                    DeviceId = dbDevice.Id,
                    Timestamp = pollTime,
                    ReadingType = "motion",
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
                if (temp.Temperature.TemperatureReport == null) continue;

                // owner.rid is the parent device ID
                var deviceName = deviceById.TryGetValue(temp.Owner.Rid, out var ownerDevice)
                    ? ownerDevice.Metadata.Name
                    : "Unknown";

                var dbDevice = await GetOrCreateDeviceAsync(db, hub, temp.Owner.Rid, "temperature_sensor", deviceName, ct);

                db.DeviceReadings.Add(new DeviceReading
                {
                    DeviceId = dbDevice.Id,
                    Timestamp = pollTime,
                    ReadingType = "temperature",
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
                foreach (var power in batteryResponse.Data)
                {
                    if (power.PowerState.BatteryLevel == null) continue;

                    var deviceName = deviceById.TryGetValue(power.Owner.Rid, out var ownerDevice)
                        ? ownerDevice.Metadata.Name
                        : "Unknown";

                    var dbDevice = await GetOrCreateDeviceAsync(db, hub, power.Owner.Rid, "battery", deviceName, ct);

                    db.DeviceReadings.Add(new DeviceReading
                    {
                        DeviceId = dbDevice.Id,
                        Timestamp = pollTime,
                        ReadingType = "battery",
                        Value = JsonSerializer.Serialize(new
                        {
                            battery_level = power.PowerState.BatteryLevel.Value,
                            battery_state = power.PowerState.BatteryState ?? "unknown"
                        })
                    });
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
            _logger.LogWarning("Hub {BridgeId}: unauthorized (401). Token may be expired", hub.HueBridgeId);
            hub.ConsecutiveFailures++;
            log.Success = false;
            log.ErrorMessage = "Unauthorized (401) - token may be expired";
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

            // Delete in batches to avoid loading excessive rows into memory at once
            const int batchSize = 1000;
            int totalReadings = 0, totalLogs = 0;

            int deleted;
            do
            {
                var batch = await db.DeviceReadings
                    .Where(r => r.Timestamp < cutoff)
                    .Take(batchSize)
                    .ToListAsync(ct);
                deleted = batch.Count;
                if (deleted > 0)
                {
                    db.DeviceReadings.RemoveRange(batch);
                    await db.SaveChangesAsync(ct);
                    totalReadings += deleted;
                }
            } while (deleted == batchSize);

            do
            {
                var batch = await db.PollingLogs
                    .Where(l => l.Timestamp < cutoff)
                    .Take(batchSize)
                    .ToListAsync(ct);
                deleted = batch.Count;
                if (deleted > 0)
                {
                    db.PollingLogs.RemoveRange(batch);
                    await db.SaveChangesAsync(ct);
                    totalLogs += deleted;
                }
            } while (deleted == batchSize);

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

    private static async Task<Device> GetOrCreateDeviceAsync(
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
            device.UpdatedAt = DateTime.UtcNow;
        }
        return device;
    }
}
