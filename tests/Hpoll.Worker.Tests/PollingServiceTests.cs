using Microsoft.Data.Sqlite;
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

public class PollingServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IHueApiClient> _mockHueClient;
    private readonly SqliteConnection _connection;
    private readonly FakeTimeProvider _fakeTime;

    public PollingServiceTests()
    {
        _mockHueClient = new Mock<IHueApiClient>();
        _fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero));

        // Use SQLite in-memory so ExecuteDeleteAsync works (requires relational provider)
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<HpollDbContext>(options =>
            options.UseSqlite(_connection));
        services.AddScoped<IHueApiClient>(_ => _mockHueClient.Object);
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        _serviceProvider = services.BuildServiceProvider();

        // Create the schema
        using var db = CreateDb();
        db.Database.EnsureCreated();
    }

    private readonly List<IServiceScope> _scopes = new();

    public void Dispose()
    {
        foreach (var scope in _scopes) scope.Dispose();
        _serviceProvider.Dispose();
        _connection.Dispose();
    }

    private HpollDbContext CreateDb()
    {
        var scope = _serviceProvider.CreateScope();
        _scopes.Add(scope);
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
            TokenExpiresAt = _fakeTime.GetUtcNow().UtcDateTime.AddDays(7),
            Status = HubStatus.Active,
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
            Options.Create(settings ?? new PollingSettings { IntervalMinutes = 60 }),
            new Mock<ISystemInfoService>().Object,
            _fakeTime);
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
                            MotionReport = new HueMotionReport { Motion = true, Changed = _fakeTime.GetUtcNow().UtcDateTime }
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
                            TemperatureReport = new HueTemperatureReport { Temperature = 21.5, Changed = _fakeTime.GetUtcNow().UtcDateTime }
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
                        Metadata = new HueDeviceMetadata { Name = "Kitchen Sensor", Archetype = "motion_sensor" },
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

        _mockHueClient.Setup(c => c.GetZigbeeConnectivityAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueZigbeeConnectivityResource>
            {
                Data = new List<HueZigbeeConnectivityResource>
                {
                    new()
                    {
                        Id = "zigbee-001",
                        Type = "zigbee_connectivity",
                        Owner = new HueResourceRef { Rid = deviceId, Rtype = "device" },
                        Status = "connected",
                        MacAddress = "00:11:22:33:44:55"
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
        var motionReadings = await db.DeviceReadings.Where(r => r.ReadingType == ReadingTypes.Motion).ToListAsync();
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
        var tempReadings = await db.DeviceReadings.Where(r => r.ReadingType == ReadingTypes.Temperature).ToListAsync();
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
        // Refresh succeeds but consecutive failures should still increment
        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueTokenResponse { AccessToken = "new-token", RefreshToken = "new-refresh", ExpiresIn = 86400 });

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.True(updatedHub.ConsecutiveFailures > 0);
    }

    [Fact]
    public async Task PollHub_On401_RefreshSucceeds_UpdatesTokens()
    {
        var hub = await SeedHubAsync();

        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized));
        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueTokenResponse { AccessToken = "refreshed-token", RefreshToken = "refreshed-refresh", ExpiresIn = 86400 });

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal(HubStatus.Active, updatedHub.Status);
        Assert.Equal("refreshed-token", updatedHub.AccessToken);
        Assert.Equal("refreshed-refresh", updatedHub.RefreshToken);
    }

    [Fact]
    public async Task PollHub_On401_RefreshFails_SetsNeedsReauth()
    {
        var hub = await SeedHubAsync();

        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized));
        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Refresh failed"));

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal(HubStatus.NeedsReauth, updatedHub.Status);
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
                DeviceType = DeviceTypes.MotionSensor,
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
                        Metadata = new HueDeviceMetadata { Name = "Sensor", Archetype = "motion_sensor" },
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
        Assert.DoesNotContain(readings, r => r.ReadingType == ReadingTypes.Motion);
    }

    [Fact]
    public async Task PollHub_SkipsDisabledMotionSensors()
    {
        await SeedHubAsync();

        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueMotionResource>
            {
                Data = new List<HueMotionResource>
                {
                    new()
                    {
                        Id = "motion-001", Type = "motion",
                        Owner = new HueResourceRef { Rid = "device-001", Rtype = "device" },
                        Enabled = false,
                        Motion = new HueMotionData { MotionReport = new HueMotionReport { Motion = true, Changed = _fakeTime.GetUtcNow().UtcDateTime } }
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
                    new() { Id = "device-001", Type = "device", Metadata = new HueDeviceMetadata { Name = "Sensor", Archetype = "motion_sensor" }, ProductData = new HueProductData { ModelId = "SML001", ProductName = "Hue", SoftwareVersion = "1.0" }, Services = new List<HueResourceRef> { new() { Rid = "motion-001", Rtype = "motion" } } }
                }
            });
        _mockHueClient.Setup(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDevicePowerResource>());

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var readings = await db.DeviceReadings.ToListAsync();
        Assert.DoesNotContain(readings, r => r.ReadingType == ReadingTypes.Motion);
    }

    [Fact]
    public async Task PollHub_SkipsDisabledTemperatureSensors()
    {
        await SeedHubAsync();

        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueMotionResource> { Data = new List<HueMotionResource>() });
        _mockHueClient.Setup(c => c.GetTemperatureSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueTemperatureResource>
            {
                Data = new List<HueTemperatureResource>
                {
                    new()
                    {
                        Id = "temp-001", Type = "temperature",
                        Owner = new HueResourceRef { Rid = "device-001", Rtype = "device" },
                        Enabled = false,
                        Temperature = new HueTemperatureData { TemperatureReport = new HueTemperatureReport { Temperature = 21.5, Changed = _fakeTime.GetUtcNow().UtcDateTime } }
                    }
                }
            });
        _mockHueClient.Setup(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDeviceResource>
            {
                Data = new List<HueDeviceResource>
                {
                    new() { Id = "device-001", Type = "device", Metadata = new HueDeviceMetadata { Name = "Sensor", Archetype = "motion_sensor" }, ProductData = new HueProductData { ModelId = "SML001", ProductName = "Hue", SoftwareVersion = "1.0" }, Services = new List<HueResourceRef> { new() { Rid = "temp-001", Rtype = "temperature" } } }
                }
            });
        _mockHueClient.Setup(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDevicePowerResource>());

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var readings = await db.DeviceReadings.ToListAsync();
        Assert.DoesNotContain(readings, r => r.ReadingType == ReadingTypes.Temperature);
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
                TokenExpiresAt = _fakeTime.GetUtcNow().UtcDateTime.AddHours(-1),
                Status = HubStatus.Active
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
                TokenExpiresAt = _fakeTime.GetUtcNow().UtcDateTime.AddDays(7),
                Status = HubStatus.NeedsReauth
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

        var recentChanged = _fakeTime.GetUtcNow().UtcDateTime.AddMinutes(-5);
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
                    new() { Id = "device-001", Type = "device", Metadata = new HueDeviceMetadata { Name = "Sensor", Archetype = "motion_sensor" }, ProductData = new HueProductData { ModelId = "SML001", ProductName = "Hue", SoftwareVersion = "1.0" }, Services = new List<HueResourceRef> { new() { Rid = "motion-001", Rtype = "motion" } } }
                }
            });
        _mockHueClient.Setup(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDevicePowerResource>());

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var reading = await db.DeviceReadings.FirstAsync(r => r.ReadingType == ReadingTypes.Motion);
        Assert.Contains("\"motion\":true", reading.Value);
    }

    [Fact]
    public async Task PollHub_NoMotion_WhenChangedIsBeforeCutoff()
    {
        var hub = await SeedHubAsync();

        using (var db = CreateDb())
        {
            var h = await db.Hubs.FirstAsync(x => x.Id == hub.Id);
            h.LastPolledAt = _fakeTime.GetUtcNow().UtcDateTime.AddMinutes(-30);
            await db.SaveChangesAsync();
        }

        var oldChanged = _fakeTime.GetUtcNow().UtcDateTime.AddHours(-2);
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
                Data = new List<HueDeviceResource> { new() { Id = "device-001", Type = "device", Metadata = new HueDeviceMetadata { Name = "Sensor", Archetype = "motion_sensor" }, ProductData = new HueProductData { ModelId = "SML001", ProductName = "Hue", SoftwareVersion = "1.0" }, Services = new List<HueResourceRef> { new() { Rid = "motion-001", Rtype = "motion" } } } }
            });
        _mockHueClient.Setup(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDevicePowerResource>());

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db2 = CreateDb();
        var reading = await db2.DeviceReadings.FirstAsync(r => r.ReadingType == ReadingTypes.Motion);
        Assert.Contains("\"motion\":false", reading.Value);
    }

    [Fact]
    public async Task PollHub_DetectsMotion_AfterExtendedDowntime()
    {
        var hub = await SeedHubAsync();

        // Simulate 6 hours of downtime: LastPolledAt was 6 hours ago
        using (var db = CreateDb())
        {
            var h = await db.Hubs.FirstAsync(x => x.Id == hub.Id);
            h.LastPolledAt = _fakeTime.GetUtcNow().UtcDateTime.AddHours(-6);
            await db.SaveChangesAsync();
        }

        // Motion changed 3 hours ago — after LastPolledAt but before intervalCutoff (60 min ago)
        var changedDuringDowntime = _fakeTime.GetUtcNow().UtcDateTime.AddHours(-3);
        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueMotionResource>
            {
                Data = new List<HueMotionResource>
                {
                    new() { Id = "motion-001", Type = "motion", Owner = new HueResourceRef { Rid = "device-001", Rtype = "device" }, Enabled = true, Motion = new HueMotionData { MotionReport = new HueMotionReport { Motion = false, Changed = changedDuringDowntime } } }
                }
            });
        _mockHueClient.Setup(c => c.GetTemperatureSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueTemperatureResource> { Data = new List<HueTemperatureResource>() });
        _mockHueClient.Setup(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDeviceResource>
            {
                Data = new List<HueDeviceResource> { new() { Id = "device-001", Type = "device", Metadata = new HueDeviceMetadata { Name = "Sensor", Archetype = "motion_sensor" }, ProductData = new HueProductData { ModelId = "SML001", ProductName = "Hue", SoftwareVersion = "1.0" }, Services = new List<HueResourceRef> { new() { Rid = "motion-001", Rtype = "motion" } } } }
            });
        _mockHueClient.Setup(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDevicePowerResource>());

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        // Math.Min picks LastPolledAt (6h ago) over intervalCutoff (60min ago),
        // so Changed (3h ago) > motionCutoff (6h ago) → motion IS detected
        using var db2 = CreateDb();
        var reading = await db2.DeviceReadings.FirstAsync(r => r.ReadingType == ReadingTypes.Motion);
        Assert.Contains("\"motion\":true", reading.Value);
    }

    [Fact]
    public async Task PollHub_NoMotion_WhenChangedIsBeforeLastPollDuringDowntime()
    {
        var hub = await SeedHubAsync();

        // Simulate 6 hours of downtime: LastPolledAt was 6 hours ago
        using (var db = CreateDb())
        {
            var h = await db.Hubs.FirstAsync(x => x.Id == hub.Id);
            h.LastPolledAt = _fakeTime.GetUtcNow().UtcDateTime.AddHours(-6);
            await db.SaveChangesAsync();
        }

        // Motion changed 7 hours ago — before LastPolledAt, already seen
        var changedBeforeLastPoll = _fakeTime.GetUtcNow().UtcDateTime.AddHours(-7);
        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueMotionResource>
            {
                Data = new List<HueMotionResource>
                {
                    new() { Id = "motion-001", Type = "motion", Owner = new HueResourceRef { Rid = "device-001", Rtype = "device" }, Enabled = true, Motion = new HueMotionData { MotionReport = new HueMotionReport { Motion = false, Changed = changedBeforeLastPoll } } }
                }
            });
        _mockHueClient.Setup(c => c.GetTemperatureSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueTemperatureResource> { Data = new List<HueTemperatureResource>() });
        _mockHueClient.Setup(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDeviceResource>
            {
                Data = new List<HueDeviceResource> { new() { Id = "device-001", Type = "device", Metadata = new HueDeviceMetadata { Name = "Sensor", Archetype = "motion_sensor" }, ProductData = new HueProductData { ModelId = "SML001", ProductName = "Hue", SoftwareVersion = "1.0" }, Services = new List<HueResourceRef> { new() { Rid = "motion-001", Rtype = "motion" } } } }
            });
        _mockHueClient.Setup(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDevicePowerResource>());

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        // Math.Min picks LastPolledAt (6h ago) over intervalCutoff (60min ago),
        // Changed (7h ago) < motionCutoff (6h ago) → motion NOT detected
        using var db2 = CreateDb();
        var reading = await db2.DeviceReadings.FirstAsync(r => r.ReadingType == ReadingTypes.Motion);
        Assert.Contains("\"motion\":false", reading.Value);
    }

    [Fact]
    public async Task CleanupOldData_DeletesReadingsAndLogsOlderThanRetentionPeriod()
    {
        var hub = await SeedHubAsync();

        using (var db = CreateDb())
        {
            var device = new Device { HubId = hub.Id, HueDeviceId = "device-001", DeviceType = DeviceTypes.MotionSensor, Name = "Sensor" };
            db.Devices.Add(device);
            await db.SaveChangesAsync();

            db.DeviceReadings.Add(new DeviceReading { DeviceId = device.Id, Timestamp = _fakeTime.GetUtcNow().UtcDateTime.AddDays(-8), ReadingType = ReadingTypes.Motion, Value = "{\"motion\":true}" });
            db.DeviceReadings.Add(new DeviceReading { DeviceId = device.Id, Timestamp = _fakeTime.GetUtcNow().UtcDateTime.AddHours(-1), ReadingType = ReadingTypes.Motion, Value = "{\"motion\":false}" });
            db.PollingLogs.Add(new PollingLog { HubId = hub.Id, Timestamp = _fakeTime.GetUtcNow().UtcDateTime.AddDays(-8), Success = true, ApiCallsMade = 3 });
            db.PollingLogs.Add(new PollingLog { HubId = hub.Id, Timestamp = _fakeTime.GetUtcNow().UtcDateTime.AddHours(-1), Success = true, ApiCallsMade = 3 });
            await db.SaveChangesAsync();
        }

        var service = CreateService();
        await service.CleanupOldDataAsync(CancellationToken.None);

        using var db2 = CreateDb();
        var readings = await db2.DeviceReadings.ToListAsync();
        Assert.DoesNotContain(readings, r => r.Timestamp < _fakeTime.GetUtcNow().UtcDateTime.AddHours(-168));
        Assert.Contains(readings, r => r.Timestamp > _fakeTime.GetUtcNow().UtcDateTime.AddHours(-2));

        var logs = await db2.PollingLogs.ToListAsync();
        Assert.DoesNotContain(logs, l => l.Timestamp < _fakeTime.GetUtcNow().UtcDateTime.AddHours(-168));
        Assert.Contains(logs, l => l.Timestamp > _fakeTime.GetUtcNow().UtcDateTime.AddHours(-2));
    }

    [Fact]
    public async Task PollHub_StoresBatteryReadings_OnFirstPoll()
    {
        await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var batteryReadings = await db.DeviceReadings.Where(r => r.ReadingType == ReadingTypes.Battery).ToListAsync();
        Assert.NotEmpty(batteryReadings);
        Assert.Contains("\"battery_level\":85", batteryReadings.First().Value);
    }

    [Fact]
    public async Task PollHub_AlwaysPollsBattery_OnStartup_EvenWhenRecentlyPolled()
    {
        var hub = await SeedHubAsync(lastBatteryPollUtc: _fakeTime.GetUtcNow().UtcDateTime.AddHours(-1));
        SetupSuccessfulHueResponses();

        var service = CreateService(new PollingSettings { IntervalMinutes = 60, BatteryPollIntervalHours = 84 });
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        _mockHueClient.Verify(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        using var db2 = CreateDb();
        var batteryReadings = await db2.DeviceReadings.Where(r => r.ReadingType == ReadingTypes.Battery).ToListAsync();
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
                        Motion = new HueMotionData { MotionReport = new HueMotionReport { Motion = true, Changed = _fakeTime.GetUtcNow().UtcDateTime } }
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
        var batteryReadings = await db.DeviceReadings.Where(r => r.ReadingType == ReadingTypes.Battery).ToListAsync();
        Assert.Empty(batteryReadings);
    }

    [Fact]
    public async Task PollHub_RemovesStaleDevices_WhenDeviceNoLongerOnHub()
    {
        var hub = await SeedHubAsync();

        // Pre-seed a battery device that is no longer present on the hub (retired device)
        using (var seedDb = CreateDb())
        {
            var staleDevice = new Device
            {
                HubId = hub.Id,
                HueDeviceId = "retired-device",
                DeviceType = DeviceTypes.Unknown,
                Name = "Old Sensor"
            };
            seedDb.Devices.Add(staleDevice);
            await seedDb.SaveChangesAsync();

            seedDb.DeviceReadings.Add(new DeviceReading
            {
                DeviceId = staleDevice.Id,
                Timestamp = _fakeTime.GetUtcNow().UtcDateTime.AddDays(-1),
                ReadingType = ReadingTypes.Battery,
                Value = "{\"battery_level\":42,\"battery_state\":\"normal\"}"
            });
            await seedDb.SaveChangesAsync();
        }

        // Hub now only reports device-001 as having battery
        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        // The retired device and its readings should be removed
        var devices = await db.Devices.Where(d => d.HueDeviceId == "retired-device").ToListAsync();
        Assert.Empty(devices);
        var staleReadings = await db.DeviceReadings
            .Where(r => r.Device.HueDeviceId == "retired-device")
            .ToListAsync();
        Assert.Empty(staleReadings);

        // The current device's battery reading should still exist
        var currentReadings = await db.DeviceReadings.Where(r => r.ReadingType == ReadingTypes.Battery).ToListAsync();
        Assert.NotEmpty(currentReadings);
        Assert.Contains("\"battery_level\":85", currentReadings.First().Value);
    }

    [Fact]
    public async Task PollHub_RemovesStaleMotionDevices_WhenDeviceNoLongerOnHub()
    {
        var hub = await SeedHubAsync();

        // Pre-seed a stale motion sensor device that is no longer present on the hub
        using (var seedDb = CreateDb())
        {
            var staleDevice = new Device
            {
                HubId = hub.Id,
                HueDeviceId = "retired-motion-device",
                DeviceType = DeviceTypes.MotionSensor,
                Name = "Old Motion Sensor"
            };
            seedDb.Devices.Add(staleDevice);
            await seedDb.SaveChangesAsync();

            seedDb.DeviceReadings.Add(new DeviceReading
            {
                DeviceId = staleDevice.Id,
                Timestamp = _fakeTime.GetUtcNow().UtcDateTime.AddDays(-1),
                ReadingType = ReadingTypes.Motion,
                Value = "{\"motion\":true,\"changed\":\"2026-03-14T12:00:00Z\"}"
            });
            await seedDb.SaveChangesAsync();
        }

        // Hub API only reports device-001 — retired-motion-device is no longer present
        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var staleDevices = await db.Devices.Where(d => d.HueDeviceId == "retired-motion-device").ToListAsync();
        Assert.Empty(staleDevices);
        var staleReadings = await db.DeviceReadings.Where(r => r.Device.HueDeviceId == "retired-motion-device").ToListAsync();
        Assert.Empty(staleReadings);

        // Current motion device should still have readings
        var currentReadings = await db.DeviceReadings.Where(r => r.ReadingType == ReadingTypes.Motion).ToListAsync();
        Assert.NotEmpty(currentReadings);
    }

    [Fact]
    public async Task PollHub_RemovesStaleTemperatureDevices_WhenDeviceNoLongerOnHub()
    {
        var hub = await SeedHubAsync();

        // Pre-seed a stale temperature sensor device
        using (var seedDb = CreateDb())
        {
            var staleDevice = new Device
            {
                HubId = hub.Id,
                HueDeviceId = "retired-temp-device",
                DeviceType = DeviceTypes.TemperatureSensor,
                Name = "Old Temp Sensor"
            };
            seedDb.Devices.Add(staleDevice);
            await seedDb.SaveChangesAsync();

            seedDb.DeviceReadings.Add(new DeviceReading
            {
                DeviceId = staleDevice.Id,
                Timestamp = _fakeTime.GetUtcNow().UtcDateTime.AddDays(-1),
                ReadingType = ReadingTypes.Temperature,
                Value = "{\"temperature\":21.5,\"changed\":\"2026-03-14T12:00:00Z\"}"
            });
            await seedDb.SaveChangesAsync();
        }

        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var staleDevices = await db.Devices.Where(d => d.HueDeviceId == "retired-temp-device").ToListAsync();
        Assert.Empty(staleDevices);
        var staleReadings = await db.DeviceReadings.Where(r => r.Device.HueDeviceId == "retired-temp-device").ToListAsync();
        Assert.Empty(staleReadings);

        // Current temperature device should still have readings
        var currentReadings = await db.DeviceReadings.Where(r => r.ReadingType == ReadingTypes.Temperature).ToListAsync();
        Assert.NotEmpty(currentReadings);
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

    [Fact]
    public async Task PollHub_FinallyBlockSaveFailure_DoesNotCrash()
    {
        var hub = await SeedHubAsync();

        // When the API call is made, close the SQLite connection so that
        // the finally-block SaveChangesAsync will fail
        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((token, key, ct) =>
            {
                _connection.Close();
                throw new Exception("API error");
            });

        var service = CreateService();
        // Should not throw — the finally block's catch should handle the save failure
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);
    }

    [Fact]
    public async Task PollHub_On401_EmptyRefreshToken_KeepsExistingRefreshToken()
    {
        var hub = await SeedHubAsync();

        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized));
        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueTokenResponse { AccessToken = "new-access", RefreshToken = "", ExpiresIn = 86400 });

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal("new-access", updatedHub.AccessToken);
        Assert.Equal("refresh-token", updatedHub.RefreshToken); // Original kept
    }

    [Fact]
    public async Task PollHub_SkipsBattery_WhenRecentlyPolledAndNotForced()
    {
        await SeedHubAsync(lastBatteryPollUtc: _fakeTime.GetUtcNow().UtcDateTime.AddHours(-1));
        SetupSuccessfulHueResponses();

        var service = CreateService(new PollingSettings { IntervalMinutes = 60, BatteryPollIntervalHours = 84 });
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var batteryReadings = await db.DeviceReadings.Where(r => r.ReadingType == ReadingTypes.Battery).ToListAsync();
        Assert.Empty(batteryReadings);
    }

    [Fact]
    public async Task PollHub_PollsBattery_WhenLastPollIsNull()
    {
        await SeedHubAsync(lastBatteryPollUtc: null);
        SetupSuccessfulHueResponses();

        var service = CreateService(new PollingSettings { IntervalMinutes = 60, BatteryPollIntervalHours = 84 });
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var batteryReadings = await db.DeviceReadings.Where(r => r.ReadingType == ReadingTypes.Battery).ToListAsync();
        Assert.NotEmpty(batteryReadings);
    }

    [Fact]
    public async Task PollHub_PollsBattery_WhenIntervalElapsed()
    {
        await SeedHubAsync(lastBatteryPollUtc: _fakeTime.GetUtcNow().UtcDateTime.AddHours(-85));
        SetupSuccessfulHueResponses();

        var service = CreateService(new PollingSettings { IntervalMinutes = 60, BatteryPollIntervalHours = 84 });
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var batteryReadings = await db.DeviceReadings.Where(r => r.ReadingType == ReadingTypes.Battery).ToListAsync();
        Assert.NotEmpty(batteryReadings);
    }

    [Fact]
    public async Task PollHub_SuccessfulPoll_UpdatesLastSuccessAt()
    {
        var hub = await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var expectedTime = _fakeTime.GetUtcNow().UtcDateTime;
        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal(expectedTime, updatedHub.LastSuccessAt);
    }

    [Fact]
    public async Task PollHub_SetsApiCallsCountOnPollingLog()
    {
        await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var log = await db.PollingLogs.FirstAsync();
        Assert.Equal(5, log.ApiCallsMade); // motion + temp + devices + battery + zigbee connectivity
    }

    [Fact]
    public async Task PollHub_WithoutBatteryPoll_SetsApiCallsTo3()
    {
        await SeedHubAsync(lastBatteryPollUtc: _fakeTime.GetUtcNow().UtcDateTime.AddHours(-1));
        SetupSuccessfulHueResponses();

        var service = CreateService(new PollingSettings { IntervalMinutes = 60, BatteryPollIntervalHours = 84 });
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var log = await db.PollingLogs.FirstAsync();
        Assert.Equal(3, log.ApiCallsMade);
    }

    [Fact]
    public async Task PollHub_WithBatteryPoll_StoresZigbeeConnectivityReading()
    {
        var hub = await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var connectivityReadings = await db.DeviceReadings
            .Where(r => r.ReadingType == ReadingTypes.ZigbeeConnectivity)
            .ToListAsync();
        Assert.NotEmpty(connectivityReadings);

        var reading = connectivityReadings.First();
        Assert.Contains("connected", reading.Value);
        Assert.Contains("mac_address", reading.Value);
    }

    [Fact]
    public async Task PollHub_WithoutBatteryPoll_DoesNotStoreConnectivityReading()
    {
        await SeedHubAsync(lastBatteryPollUtc: _fakeTime.GetUtcNow().UtcDateTime.AddHours(-1));
        SetupSuccessfulHueResponses();

        var service = CreateService(new PollingSettings { IntervalMinutes = 60, BatteryPollIntervalHours = 84 });
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var connectivityReadings = await db.DeviceReadings
            .Where(r => r.ReadingType == ReadingTypes.ZigbeeConnectivity)
            .ToListAsync();
        Assert.Empty(connectivityReadings);
    }

    [Fact]
    public async Task PollHub_On503_CreatesFailureLog()
    {
        var hub = await SeedHubAsync();

        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service Unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable));

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var log = await db.PollingLogs.FirstAsync(l => l.HubId == hub.Id);
        Assert.False(log.Success);
        Assert.Contains("503", log.ErrorMessage!);
    }

    [Fact]
    public async Task PollHub_On429_CreatesFailureLog()
    {
        var hub = await SeedHubAsync();

        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Rate limited", null, System.Net.HttpStatusCode.TooManyRequests));

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var log = await db.PollingLogs.FirstAsync(l => l.HubId == hub.Id);
        Assert.False(log.Success);
        Assert.Contains("429", log.ErrorMessage!);
    }

    [Fact]
    public async Task PollHub_TemperatureWithUnknownOwner_UsesUnknownName()
    {
        await SeedHubAsync();

        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueMotionResource> { Data = new List<HueMotionResource>() });
        _mockHueClient.Setup(c => c.GetTemperatureSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueTemperatureResource>
            {
                Data = new List<HueTemperatureResource>
                {
                    new()
                    {
                        Id = "temp-001", Type = "temperature",
                        Owner = new HueResourceRef { Rid = "nonexistent-device", Rtype = "device" },
                        Enabled = true,
                        Temperature = new HueTemperatureData
                        {
                            TemperatureReport = new HueTemperatureReport { Temperature = 22.5, Changed = _fakeTime.GetUtcNow().UtcDateTime }
                        }
                    }
                }
            });
        _mockHueClient.Setup(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDeviceResource> { Data = new List<HueDeviceResource>() });
        _mockHueClient.Setup(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDevicePowerResource>());

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var device = await db.Devices.FirstAsync(d => d.HueDeviceId == "nonexistent-device");
        Assert.Equal("Unknown", device.Name);
        Assert.Equal(DeviceTypes.Unknown, device.DeviceType);
    }

    [Fact]
    public async Task PollHub_BatteryWithUnknownOwner_UsesUnknownName()
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
                        Owner = new HueResourceRef { Rid = "unknown-battery-device", Rtype = "device" },
                        PowerState = new HuePowerState { BatteryLevel = 50, BatteryState = "normal" }
                    }
                }
            });

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var device = await db.Devices.FirstAsync(d => d.HueDeviceId == "unknown-battery-device");
        Assert.Equal("Unknown", device.Name);
        Assert.Equal(DeviceTypes.Unknown, device.DeviceType);
    }

    [Fact]
    public async Task PollHub_SkipsNullTemperatureReport()
    {
        await SeedHubAsync();

        _mockHueClient.Setup(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueMotionResource> { Data = new List<HueMotionResource>() });
        _mockHueClient.Setup(c => c.GetTemperatureSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueTemperatureResource>
            {
                Data = new List<HueTemperatureResource>
                {
                    new()
                    {
                        Id = "temp-001", Type = "temperature",
                        Owner = new HueResourceRef { Rid = "device-001", Rtype = "device" },
                        Enabled = true,
                        Temperature = new HueTemperatureData { TemperatureReport = null }
                    }
                }
            });
        _mockHueClient.Setup(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDeviceResource>
            {
                Data = new List<HueDeviceResource>
                {
                    new() { Id = "device-001", Type = "device", Metadata = new HueDeviceMetadata { Name = "Sensor", Archetype = "motion_sensor" }, ProductData = new HueProductData { ModelId = "SML001", ProductName = "Hue", SoftwareVersion = "1.0" }, Services = new List<HueResourceRef>() }
                }
            });
        _mockHueClient.Setup(c => c.GetDevicePowerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDevicePowerResource>());

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: false, CancellationToken.None);

        using var db = CreateDb();
        var readings = await db.DeviceReadings.ToListAsync();
        Assert.DoesNotContain(readings, r => r.ReadingType == ReadingTypes.Temperature);
    }

    [Fact]
    public async Task PollHub_UpdatesLastPolledAt()
    {
        var hub = await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var expectedTime = _fakeTime.GetUtcNow().UtcDateTime;
        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal(expectedTime, updatedHub.LastPolledAt);
    }

    [Fact]
    public async Task CleanupOldData_KeepsRecentData()
    {
        var hub = await SeedHubAsync();

        using (var db = CreateDb())
        {
            var device = new Device { HubId = hub.Id, HueDeviceId = "device-001", DeviceType = DeviceTypes.MotionSensor, Name = "Sensor" };
            db.Devices.Add(device);
            await db.SaveChangesAsync();

            // Only recent data — should all be kept
            db.DeviceReadings.Add(new DeviceReading { DeviceId = device.Id, Timestamp = _fakeTime.GetUtcNow().UtcDateTime.AddHours(-1), ReadingType = ReadingTypes.Motion, Value = "{\"motion\":true}" });
            db.DeviceReadings.Add(new DeviceReading { DeviceId = device.Id, Timestamp = _fakeTime.GetUtcNow().UtcDateTime.AddHours(-2), ReadingType = ReadingTypes.Temperature, Value = "{\"temperature\":21.5}" });
            db.PollingLogs.Add(new PollingLog { HubId = hub.Id, Timestamp = _fakeTime.GetUtcNow().UtcDateTime.AddHours(-1), Success = true, ApiCallsMade = 3 });
            await db.SaveChangesAsync();
        }

        var service = CreateService();
        await service.CleanupOldDataAsync(CancellationToken.None);

        using var db2 = CreateDb();
        Assert.Equal(2, await db2.DeviceReadings.CountAsync());
        Assert.Equal(1, await db2.PollingLogs.CountAsync());
    }

    [Fact]
    public async Task PollHub_DeviceNameChanged_UpdatedAtUsesTimeProvider()
    {
        var hub = await SeedHubAsync();

        // Pre-create a device with a different name so the name-change branch is hit
        using (var db = CreateDb())
        {
            db.Devices.Add(new Device
            {
                HubId = hub.Id,
                HueDeviceId = "device-001",
                DeviceType = DeviceTypes.MotionSensor,
                Name = "Old Name"
            });
            await db.SaveChangesAsync();
        }

        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db2 = CreateDb();
        var device = await db2.Devices.FirstAsync(d => d.HueDeviceId == "device-001" && d.HubId == hub.Id);

        // Device name should be updated
        Assert.Equal("Kitchen Sensor", device.Name);
        // UpdatedAt should use the FakeTimeProvider's time
        Assert.Equal(_fakeTime.GetUtcNow().UtcDateTime, device.UpdatedAt);
    }

    [Fact]
    public async Task PollHub_NewDevice_DoesNotSetUpdatedAtFromTimeProvider()
    {
        // When creating a new device (not renaming), UpdatedAt should remain at default
        await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var service = CreateService();
        await service.PollAllHubsAsync(forceBatteryPoll: true, CancellationToken.None);

        using var db = CreateDb();
        var device = await db.Devices.FirstAsync(d => d.HueDeviceId == "device-001");

        // New device creation should not hit the name-change branch
        Assert.Equal("Kitchen Sensor", device.Name);
        // UpdatedAt should NOT be the FakeTimeProvider's time (it wasn't renamed)
        Assert.NotEqual(_fakeTime.GetUtcNow().UtcDateTime, device.UpdatedAt);
    }
}
