namespace Hpoll.Worker.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Data;
using Hpoll.Data.Entities;
using System.Text.Json;

public class PollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PollingService> _logger;
    private readonly PollingSettings _settings;

    public PollingService(
        IServiceScopeFactory scopeFactory,
        ILogger<PollingService> logger,
        IOptions<PollingSettings> settings)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Polling service started. Interval: {Interval} minutes", _settings.IntervalMinutes);

        // Run immediately on startup, then on timer
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAllHubsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in polling cycle");
            }

            await Task.Delay(TimeSpan.FromMinutes(_settings.IntervalMinutes), stoppingToken);
        }
    }

    private async Task PollAllHubsAsync(CancellationToken ct)
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
            await PollHubAsync(db, hueClient, hub, ct);
        }
    }

    private async Task PollHubAsync(HpollDbContext db, IHueApiClient hueClient, Hub hub, CancellationToken ct)
    {
        var log = new PollingLog { HubId = hub.Id, Timestamp = DateTime.UtcNow };
        int apiCalls = 0;

        try
        {
            // 1. Fetch motion sensors
            var motionResponse = await hueClient.GetMotionSensorsAsync(hub.AccessToken, hub.HueApplicationKey, ct);
            apiCalls++;

            // 2. Fetch temperature sensors
            var tempResponse = await hueClient.GetTemperatureSensorsAsync(hub.AccessToken, hub.HueApplicationKey, ct);
            apiCalls++;

            // 3. Fetch devices for name resolution
            var deviceResponse = await hueClient.GetDevicesAsync(hub.AccessToken, hub.HueApplicationKey, ct);
            apiCalls++;

            // Build device lookup: device ID -> device (for owner.rid lookups)
            // Also build service ID -> device (for cross-referencing by sensor ID)
            var deviceById = deviceResponse.Data.ToDictionary(d => d.Id, d => d);
            var deviceByServiceId = new Dictionary<string, Hpoll.Core.Models.HueDeviceResource>();
            foreach (var device in deviceResponse.Data)
            {
                foreach (var service in device.Services)
                {
                    deviceByServiceId[service.Rid] = device;
                }
            }

            // Process motion readings
            foreach (var motion in motionResponse.Data)
            {
                if (motion.Motion.MotionReport == null) continue;

                // owner.rid is the parent device ID
                var deviceName = deviceById.TryGetValue(motion.Owner.Rid, out var ownerDevice)
                    ? ownerDevice.Metadata.Name
                    : "Unknown";

                var dbDevice = await GetOrCreateDeviceAsync(db, hub, motion.Owner.Rid, "motion_sensor", deviceName, ct);

                db.DeviceReadings.Add(new DeviceReading
                {
                    DeviceId = dbDevice.Id,
                    Timestamp = DateTime.UtcNow,
                    ReadingType = "motion",
                    Value = JsonSerializer.Serialize(new
                    {
                        motion = motion.Motion.MotionReport.Motion,
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
                    Timestamp = DateTime.UtcNow,
                    ReadingType = "temperature",
                    Value = JsonSerializer.Serialize(new
                    {
                        temperature = temp.Temperature.TemperatureReport.Temperature,
                        changed = temp.Temperature.TemperatureReport.Changed
                    })
                });
            }

            await db.SaveChangesAsync(ct);

            hub.LastPolledAt = DateTime.UtcNow;
            hub.LastSuccessAt = DateTime.UtcNow;
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
            log.ErrorMessage = ex.Message;
        }
        finally
        {
            hub.LastPolledAt = DateTime.UtcNow;
            hub.UpdatedAt = DateTime.UtcNow;
            log.ApiCallsMade = apiCalls;
            db.PollingLogs.Add(log);
            await db.SaveChangesAsync(ct);
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
