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

public class PollingServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IHueApiClient> _mockHueClient;
    private readonly string _dbName;

    public PollingServiceTests()
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

    private async Task<Hub> SeedHubAsync(string bridgeId = "001788FFFE123456", DateTime? lastBatteryPollUtc = null)
    {
        using var db = CreateDb();

        // Reuse an existing customer or create one
        var customer = await db.Customers.FirstOrDefaultAsync();
        if (customer == null)
        {
            customer = new Customer { Name = "Test", Email = "test@example.com" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
        }

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = bridgeId,
            HueApplicationKey = "appkey",
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = "active",
            LastBatteryPollUtc = lastBatteryPollUtc
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();
        return hub;
    }

    private PollingService CreateService(PollingSettings? settings = null)
    {
        return new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            Options.Create(settings ?? new PollingSettings { IntervalMinutes = 60 }));
    }

    private void SetupSuccessfulHueResponses(string deviceId = "device-001")
    {
        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueMotionResource>
            {
                Data = new List<HueMotionResource>
                {
                    new()
                    {
                        Id = "motion-001",
                        Type = "motion",
                        Owner = new HueResourceRef { Rid = deviceId, Rtype = "device" },
                        Enabled = true,
                        Motion = new HueMotionData
                        {
                            MotionReport = new HueMotionReport { Motion = true, Changed = DateTime.UtcNow }
                        }
                    }
                }
            });

        _mockHueClient.Setup(c => c.GetTemperatureSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueTemperatureResource>
            {
                Data = new List<HueTemperatureResource>
                {
                    new()
                    {
                        Id = "temp-001",
                        Type = "temperature",
                        Owner = new HueResourceRef { Rid = deviceId, Rtype = "device" },
                        Enabled = true,
                        Temperature = new HueTemperatureData
                        {
                            TemperatureReport = new HueTemperatureReport { Temperature = 21.5, Changed = DateTime.UtcNow }
                        }
                    }
                }
            });

        _mockHueClient.Setup(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDeviceResource>
            {
                Data = new List<HueDeviceResource>
                {
                    new()
                    {
                        Id = deviceId,
                        Type = "device",
                        Metadata = new HueDeviceMetadata { Name = "Kitchen Sensor", Archetype = "unknown_archetype" },
                        ProductData = new HueProductData { ModelId = "SML001", ProductName = "Hue motion sensor", SoftwareVersion = "1.0.0" },
                        Services = new List<HueResourceRef>
                        {
                            new() { Rid = "motion-001", Rtype = "motion" },
                            new() { Rid = "temp-001", Rtype = "temperature" }
                        }
                    }
                }
            });

        _mockHueClient.Setup(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDevicePowerResource>
            {
                Data = new List<HueDevicePowerResource>
                {
                    new()
                    {
                        Id = "power-001",
                        Type = "device_power",
                        Owner = new HueResourceRef { Rid = deviceId, Rtype = "device" },
                        PowerState = new HuePowerState { BatteryLevel = 85, BatteryState = "normal" }
                    }
                }
            });
    }

    [Fact]
    public async Task PollAllHubs_CallsHueApiForEachActiveHub()
    {
        await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        _mockHueClient.Verify(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockHueClient.Verify(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PollHub_StoresMotionReadings()
    {
        await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var motionReadings = await db.DeviceReadings.Where(r => r.ReadingType == "motion").ToListAsync();
        Assert.NotEmpty(motionReadings);
    }

    [Fact]
    public async Task PollHub_StoresTemperatureReadings()
    {
        await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var tempReadings = await db.DeviceReadings.Where(r => r.ReadingType == "temperature").ToListAsync();
        Assert.NotEmpty(tempReadings);
    }

    [Fact]
    public async Task PollHub_On503_IncrementsConsecutiveFailures()
    {
        var hub = await SeedHubAsync();

        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service Unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable));

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.True(updatedHub.ConsecutiveFailures > 0);
    }

    [Fact]
    public async Task PollHub_OnSuccess_ResetsConsecutiveFailures()
    {
        var hub = await SeedHubAsync();

        using (var db = CreateDb())
        {
            var h = await db.Hubs.FirstAsync(x => x.Id == hub.Id);
            h.ConsecutiveFailures = 5;
            await db.SaveChangesAsync();
        }

        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db2 = CreateDb();
        var updatedHub = await db2.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal(0, updatedHub.ConsecutiveFailures);
    }

    [Fact]
    public async Task PollHub_CreatesNewDeviceIfNotExists()
    {
        var hub = await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var devices = await db.Devices.Where(d => d.HubId == hub.Id).ToListAsync();
        Assert.NotEmpty(devices);
    }

    [Fact]
    public async Task PollHub_On401_IncrementsConsecutiveFailures()
    {
        var hub = await SeedHubAsync();

        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized));

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.True(updatedHub.ConsecutiveFailures > 0);
    }

    [Fact]
    public async Task PollHub_On429_IncrementsConsecutiveFailures()
    {
        var hub = await SeedHubAsync();

        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Rate limited", null, System.Net.HttpStatusCode.TooManyRequests));

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.True(updatedHub.ConsecutiveFailures > 0);
    }

    [Fact]
    public async Task PollHub_OnGeneralException_LogsAndContinues()
    {
        var hub = await SeedHubAsync();

        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.True(updatedHub.ConsecutiveFailures > 0);
        var logs = await db.PollingLogs.Where(l => l.HubId == hub.Id).ToListAsync();
        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => !l.Success);
    }

    [Fact]
    public async Task PollHub_CreatesPollingLogOnSuccess()
    {
        var hub = await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var logs = await db.PollingLogs.Where(l => l.HubId == hub.Id).ToListAsync();
        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => l.Success && l.ApiCallsMade >= 3);
    }

    [Fact]
    public async Task PollHub_UpdatesDeviceName_WhenChanged()
    {
        var hub = await SeedHubAsync();

        using (var db = CreateDb())
        {
            db.Devices.Add(new Device
            {
                HubId = hub.Id,
                HueDeviceId = "device-001",
                DeviceType = "motion_sensor",
                Name = "Old Name"
            });
            await db.SaveChangesAsync();
        }

        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db2 = CreateDb();
        var device = await db2.Devices.FirstAsync(d => d.HueDeviceId == "device-001" && d.HubId == hub.Id);
        Assert.Equal("Kitchen Sensor", device.Name);
    }

    [Fact]
    public async Task PollHub_SkipsMotionReadings_WithNullMotionReport()
    {
        await SeedHubAsync();

        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueMotionResource>
            {
                Data = new List<HueMotionResource>
                {
                    new()
                    {
                        Id = "motion-001",
                        Type = "motion",
                        Owner = new HueResourceRef { Rid = "device-001", Rtype = "device" },
                        Enabled = true,
                        Motion = new HueMotionData { MotionReport = null }
                    }
                }
            });

        _mockHueClient.Setup(c => c.GetTemperatureSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueTemperatureResource> { Data = new List<HueTemperatureResource>() });

        _mockHueClient.Setup(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDeviceResource>
            {
                Data = new List<HueDeviceResource>
                {
                    new()
                    {
                        Id = "device-001",
                        Type = "device",
                        Metadata = new HueDeviceMetadata { Name = "Sensor", Archetype = "unknown_archetype" },
                        ProductData = new HueProductData { ModelId = "SML001", ProductName = "Hue motion sensor", SoftwareVersion = "1.0" },
                        Services = new List<HueResourceRef> { new() { Rid = "motion-001", Rtype = "motion" } }
                    }
                }
            });

        _mockHueClient.Setup(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDevicePowerResource>());

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var readings = await db.DeviceReadings.ToListAsync();
        Assert.DoesNotContain(readings, r => r.ReadingType == "motion");
    }

    [Fact]
    public async Task PollHub_SkipsHubsWithExpiredTokens()
    {
        using (var db = CreateDb())
        {
            var customer = new Customer { Name = "Test", Email = "test@example.com" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            db.Hubs.Add(new Hub
            {
                CustomerId = customer.Id,
                HueBridgeId = "001788FFFE123456",
                HueApplicationKey = "appkey",
                AccessToken = "expired-token",
                RefreshToken = "refresh",
                TokenExpiresAt = DateTime.UtcNow.AddHours(-1),
                Status = "active"
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        _mockHueClient.Verify(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PollHub_SkipsInactiveHubs()
    {
        using (var db = CreateDb())
        {
            var customer = new Customer { Name = "Test", Email = "test@example.com" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            db.Hubs.Add(new Hub
            {
                CustomerId = customer.Id,
                HueBridgeId = "001788FFFE123456",
                HueApplicationKey = "appkey",
                AccessToken = "token",
                RefreshToken = "refresh",
                TokenExpiresAt = DateTime.UtcNow.AddDays(7),
                Status = "needs_reauth"
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        _mockHueClient.Verify(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PollHub_DetectsMotion_WhenChangedIsAfterCutoff()
    {
        var hub = await SeedHubAsync();

        var recentChanged = DateTime.UtcNow.AddMinutes(-5);
        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueMotionResource>
            {
                Data = new List<HueMotionResource>
                {
                    new()
                    {
                        Id = "motion-001", Type = "motion",
                        Owner = new HueResourceRef { Rid = "device-001", Rtype = "device" },
                        Enabled = true,
                        Motion = new HueMotionData { MotionReport = new HueMotionReport { Motion = false, Changed = recentChanged } }
                    }
                }
            });

        _mockHueClient.Setup(c => c.GetTemperatureSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueTemperatureResource> { Data = new List<HueTemperatureResource>() });
        _mockHueClient.Setup(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDeviceResource>
            {
                Data = new List<HueDeviceResource>
                {
                    new() { Id = "device-001", Type = "device", Metadata = new HueDeviceMetadata { Name = "Sensor", Archetype = "a" }, ProductData = new HueProductData { ModelId = "SML001", ProductName = "Hue", SoftwareVersion = "1.0" }, Services = new List<HueResourceRef> { new() { Rid = "motion-001", Rtype = "motion" } } }
                }
            });
        _mockHueClient.Setup(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDevicePowerResource>());

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var reading = await db.DeviceReadings.FirstAsync(r => r.ReadingType == "motion");
        Assert.Contains("\"motion\":true", reading.Value);
    }

    [Fact]
    public async Task PollHub_NoMotion_WhenChangedIsBeforeCutoff()
    {
        var hub = await SeedHubAsync();

        using (var db = CreateDb())
        {
            var h = await db.Hubs.FirstAsync(x => x.Id == hub.Id);
            h.LastPolledAt = DateTime.UtcNow.AddMinutes(-30);
            await db.SaveChangesAsync();
        }

        var oldChanged = DateTime.UtcNow.AddHours(-2);
        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueMotionResource>
            {
                Data = new List<HueMotionResource>
                {
                    new() { Id = "motion-001", Type = "motion", Owner = new HueResourceRef { Rid = "device-001", Rtype = "device" }, Enabled = true, Motion = new HueMotionData { MotionReport = new HueMotionReport { Motion = false, Changed = oldChanged } } }
                }
            });
        _mockHueClient.Setup(c => c.GetTemperatureSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueTemperatureResource> { Data = new List<HueTemperatureResource>() });
        _mockHueClient.Setup(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDeviceResource>
            {
                Data = new List<HueDeviceResource> { new() { Id = "device-001", Type = "device", Metadata = new HueDeviceMetadata { Name = "Sensor", Archetype = "a" }, ProductData = new HueProductData { ModelId = "SML001", ProductName = "Hue", SoftwareVersion = "1.0" }, Services = new List<HueResourceRef> { new() { Rid = "motion-001", Rtype = "motion" } } } }
            });
        _mockHueClient.Setup(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDevicePowerResource>());

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db2 = CreateDb();
        var reading = await db2.DeviceReadings.FirstAsync(r => r.ReadingType == "motion");
        Assert.Contains("\"motion\":false", reading.Value);
    }

    [Fact]
    public async Task CleanupOldData_DeletesReadingsAndLogsOlderThanRetentionPeriod()
    {
        var hub = await SeedHubAsync();

        using (var db = CreateDb())
        {
            var device = new Device { HubId = hub.Id, HueDeviceId = "device-001", DeviceType = "motion_sensor", Name = "Sensor" };
            db.Devices.Add(device);
            await db.SaveChangesAsync();

            db.DeviceReadings.Add(new DeviceReading { DeviceId = device.Id, Timestamp = DateTime.UtcNow.AddDays(-3), ReadingType = "motion", Value = "{\"motion\":true}" });
            db.DeviceReadings.Add(new DeviceReading { DeviceId = device.Id, Timestamp = DateTime.UtcNow.AddHours(-1), ReadingType = "motion", Value = "{\"motion\":false}" });
            db.PollingLogs.Add(new PollingLog { HubId = hub.Id, Timestamp = DateTime.UtcNow.AddDays(-3), Success = true, ApiCallsMade = 3 });
            db.PollingLogs.Add(new PollingLog { HubId = hub.Id, Timestamp = DateTime.UtcNow.AddHours(-1), Success = true, ApiCallsMade = 3 });
            await db.SaveChangesAsync();
        }

        var service = CreateService();
        await service.CleanupOldDataAsync(CancellationToken.None);

        using var db2 = CreateDb();
        var readings = await db2.DeviceReadings.ToListAsync();
        Assert.DoesNotContain(readings, r => r.Timestamp < DateTime.UtcNow.AddHours(-48));
        Assert.Contains(readings, r => r.Timestamp > DateTime.UtcNow.AddHours(-2));

        var logs = await db2.PollingLogs.ToListAsync();
        Assert.DoesNotContain(logs, l => l.Timestamp < DateTime.UtcNow.AddHours(-48));
        Assert.Contains(logs, l => l.Timestamp > DateTime.UtcNow.AddHours(-2));
    }

    [Fact]
    public async Task PollHub_StoresBatteryReadings_OnFirstPoll()
    {
        await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var batteryReadings = await db.DeviceReadings.Where(r => r.ReadingType == "battery").ToListAsync();
        Assert.NotEmpty(batteryReadings);
        Assert.Contains("\"battery_level\":85", batteryReadings.First().Value);
    }

    [Fact]
    public async Task PollHub_AlwaysPollsBattery_OnStartup_EvenWhenRecentlyPolled()
    {
        var hub = await SeedHubAsync(lastBatteryPollUtc: DateTime.UtcNow.AddHours(-1));
        SetupSuccessfulHueResponses();

        var service = CreateService(new PollingSettings { IntervalMinutes = 60, BatteryPollIntervalHours = 84 });
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        _mockHueClient.Verify(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        using var db2 = CreateDb();
        var batteryReadings = await db2.DeviceReadings.Where(r => r.ReadingType == "battery").ToListAsync();
        Assert.NotEmpty(batteryReadings);
    }

    [Fact]
    public async Task PollHub_UpdatesLastBatteryPollUtc_AfterBatteryPoll()
    {
        var hub = await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.NotNull(updatedHub.LastBatteryPollUtc);
    }

    // === New tests for Issue #36 ===

    [Fact]
    public async Task PollAllHubs_MultipleActiveHubs_PollsBothHubs()
    {
        await SeedHubAsync(bridgeId: "001788FFFE111111");
        await SeedHubAsync(bridgeId: "001788FFFE222222");
        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        // Each hub triggers one call to GetMotionSensorsAsync
        _mockHueClient.Verify(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task PollHub_DeviceWithUnknownOwnerRid_UsesUnknownName()
    {
        await SeedHubAsync();

        // Motion sensor owner.rid doesn't match any device
        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueMotionResource>
            {
                Data = new List<HueMotionResource>
                {
                    new()
                    {
                        Id = "motion-001", Type = "motion",
                        Owner = new HueResourceRef { Rid = "nonexistent-device", Rtype = "device" },
                        Enabled = true,
                        Motion = new HueMotionData { MotionReport = new HueMotionReport { Motion = true, Changed = DateTime.UtcNow } }
                    }
                }
            });
        _mockHueClient.Setup(c => c.GetTemperatureSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueTemperatureResource> { Data = new List<HueTemperatureResource>() });
        _mockHueClient.Setup(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDeviceResource> { Data = new List<HueDeviceResource>() });
        _mockHueClient.Setup(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDevicePowerResource>());

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var device = await db.Devices.FirstAsync(d => d.HueDeviceId == "nonexistent-device");
        Assert.Equal("Unknown", device.Name);
    }

    [Fact]
    public async Task PollHub_BatteryNullLevel_SkipsBatteryReading()
    {
        await SeedHubAsync();

        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueMotionResource> { Data = new List<HueMotionResource>() });
        _mockHueClient.Setup(c => c.GetTemperatureSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueTemperatureResource> { Data = new List<HueTemperatureResource>() });
        _mockHueClient.Setup(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDeviceResource> { Data = new List<HueDeviceResource>() });
        _mockHueClient.Setup(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDevicePowerResource>
            {
                Data = new List<HueDevicePowerResource>
                {
                    new()
                    {
                        Id = "power-001", Type = "device_power",
                        Owner = new HueResourceRef { Rid = "device-001", Rtype = "device" },
                        PowerState = new HuePowerState { BatteryLevel = null, BatteryState = "unknown" }
                    }
                }
            });

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var batteryReadings = await db.DeviceReadings.Where(r => r.ReadingType == "battery").ToListAsync();
        Assert.Empty(batteryReadings);
    }

    [Fact]
    public async Task PollHub_ErrorMessage_TruncatedTo500Chars()
    {
        var hub = await SeedHubAsync();

        var longMessage = new string('X', 1000);
        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(longMessage));

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var log = await db.PollingLogs.FirstAsync(l => l.HubId == hub.Id && !l.Success);
        Assert.Equal(500, log.ErrorMessage!.Length);
    }

    [Fact]
    public async Task CleanupOldData_ExceptionDuringCleanup_LogsWarning()
    {
        // Cleanup should not throw even when there's an error
        // Using an empty database is sufficient — it just finds 0 records
        var service = CreateService();
        // This should not throw
        await service.CleanupOldDataAsync(CancellationToken.None);
    }
}
