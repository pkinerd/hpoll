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
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = "active"
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();
        return hub;
    }

    private void SetupSuccessfulHueResponses()
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
                        Owner = new HueResourceRef { Rid = "device-001", Rtype = "device" },
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
                        Owner = new HueResourceRef { Rid = "device-001", Rtype = "device" },
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
                        Id = "device-001",
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
    }

    [Fact]
    public async Task PollAllHubs_CallsHueApiForEachActiveHub()
    {
        var hub = await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 1 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        _mockHueClient.Verify(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockHueClient.Verify(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PollHub_StoresMotionReadings()
    {
        var hub = await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        using var db = CreateDb();
        var motionReadings = await db.DeviceReadings.Where(r => r.ReadingType == "motion").ToListAsync();
        Assert.NotEmpty(motionReadings);
    }

    [Fact]
    public async Task PollHub_StoresTemperatureReadings()
    {
        var hub = await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

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

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.True(updatedHub.ConsecutiveFailures > 0);
    }

    [Fact]
    public async Task PollHub_OnSuccess_ResetsConsecutiveFailures()
    {
        var hub = await SeedHubAsync();

        // Set some prior failures
        using (var db = CreateDb())
        {
            var h = await db.Hubs.FirstAsync(x => x.Id == hub.Id);
            h.ConsecutiveFailures = 5;
            await db.SaveChangesAsync();
        }

        SetupSuccessfulHueResponses();

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        using var db2 = CreateDb();
        var updatedHub = await db2.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.Equal(0, updatedHub.ConsecutiveFailures);
    }

    [Fact]
    public async Task PollHub_CreatesNewDeviceIfNotExists()
    {
        var hub = await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

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

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

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

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

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

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        using var db = CreateDb();
        var updatedHub = await db.Hubs.FirstAsync(h => h.Id == hub.Id);
        Assert.True(updatedHub.ConsecutiveFailures > 0);
        // Verify polling log was created
        var logs = await db.PollingLogs.Where(l => l.HubId == hub.Id).ToListAsync();
        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => !l.Success);
    }

    [Fact]
    public async Task PollHub_CreatesPollingLogOnSuccess()
    {
        var hub = await SeedHubAsync();
        SetupSuccessfulHueResponses();

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        using var db = CreateDb();
        var logs = await db.PollingLogs.Where(l => l.HubId == hub.Id).ToListAsync();
        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => l.Success && l.ApiCallsMade == 3);
    }

    [Fact]
    public async Task PollHub_UpdatesDeviceName_WhenChanged()
    {
        var hub = await SeedHubAsync();

        // Pre-create a device with a different name
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

        SetupSuccessfulHueResponses(); // Returns "Kitchen Sensor" as the device name

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        using var db2 = CreateDb();
        var device = await db2.Devices.FirstAsync(d => d.HueDeviceId == "device-001" && d.HubId == hub.Id);
        Assert.Equal("Kitchen Sensor", device.Name);
    }

    [Fact]
    public async Task PollHub_SkipsMotionReadings_WithNullMotionReport()
    {
        var hub = await SeedHubAsync();

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

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        using var db = CreateDb();
        var readings = await db.DeviceReadings.ToListAsync();
        Assert.Empty(readings); // Null motion report should be skipped
    }

    [Fact]
    public async Task PollHub_SkipsHubsWithExpiredTokens()
    {
        // Seed a hub with an expired token
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
                TokenExpiresAt = DateTime.UtcNow.AddHours(-1), // Expired 1 hour ago
                Status = "active"
            });
            await db.SaveChangesAsync();
        }

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Hue API should never have been called for an expired token
        _mockHueClient.Verify(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PollHub_SkipsInactiveHubs()
    {
        // Seed an inactive hub
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
                Status = "needs_reauth" // Not "active"
            });
            await db.SaveChangesAsync();
        }

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Hue API should never have been called
        _mockHueClient.Verify(c => c.GetMotionSensorsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PollHub_DetectsMotion_WhenChangedIsAfterCutoff()
    {
        var hub = await SeedHubAsync();

        // Changed timestamp is recent (5 minutes ago) — within the polling interval
        var recentChanged = DateTime.UtcNow.AddMinutes(-5);
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
                        Motion = new HueMotionData
                        {
                            // motion boolean is false (already reset), but Changed is recent
                            MotionReport = new HueMotionReport { Motion = false, Changed = recentChanged }
                        }
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

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        using var db = CreateDb();
        var reading = await db.DeviceReadings.FirstAsync(r => r.ReadingType == "motion");
        // Even though the API's motion boolean was false, Changed is after the cutoff
        // so motion should be detected as true
        Assert.Contains("\"motion\":true", reading.Value);
    }

    [Fact]
    public async Task PollHub_NoMotion_WhenChangedIsBeforeCutoff()
    {
        var hub = await SeedHubAsync();

        // Set LastPolledAt to 30 minutes ago so we have a recent baseline
        using (var db = CreateDb())
        {
            var h = await db.Hubs.FirstAsync(x => x.Id == hub.Id);
            h.LastPolledAt = DateTime.UtcNow.AddMinutes(-30);
            await db.SaveChangesAsync();
        }

        // Changed timestamp is 2 hours ago — before both LastPolledAt and interval cutoff
        var oldChanged = DateTime.UtcNow.AddHours(-2);
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
                        Motion = new HueMotionData
                        {
                            MotionReport = new HueMotionReport { Motion = false, Changed = oldChanged }
                        }
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

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        using var db2 = CreateDb();
        var reading = await db2.DeviceReadings.FirstAsync(r => r.ReadingType == "motion");
        // Changed is before the cutoff — no motion detected
        Assert.Contains("\"motion\":false", reading.Value);
    }

    [Fact]
    public async Task CleanupOldData_DeletesReadingsAndLogsOlderThan30Days()
    {
        var hub = await SeedHubAsync();

        // Seed old and recent readings/logs
        using (var db = CreateDb())
        {
            var device = new Device
            {
                HubId = hub.Id,
                HueDeviceId = "device-001",
                DeviceType = "motion_sensor",
                Name = "Sensor"
            };
            db.Devices.Add(device);
            await db.SaveChangesAsync();

            // Old reading (31 days ago)
            db.DeviceReadings.Add(new DeviceReading
            {
                DeviceId = device.Id,
                Timestamp = DateTime.UtcNow.AddDays(-31),
                ReadingType = "motion",
                Value = "{\"motion\":true}"
            });

            // Recent reading (1 day ago)
            db.DeviceReadings.Add(new DeviceReading
            {
                DeviceId = device.Id,
                Timestamp = DateTime.UtcNow.AddDays(-1),
                ReadingType = "motion",
                Value = "{\"motion\":false}"
            });

            // Old polling log (31 days ago)
            db.PollingLogs.Add(new PollingLog
            {
                HubId = hub.Id,
                Timestamp = DateTime.UtcNow.AddDays(-31),
                Success = true,
                ApiCallsMade = 3
            });

            // Recent polling log (1 day ago)
            db.PollingLogs.Add(new PollingLog
            {
                HubId = hub.Id,
                Timestamp = DateTime.UtcNow.AddDays(-1),
                Success = true,
                ApiCallsMade = 3
            });

            await db.SaveChangesAsync();
        }

        SetupSuccessfulHueResponses();

        var settings = Options.Create(new PollingSettings { IntervalMinutes = 60 });
        var service = new PollingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PollingService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(2000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        using var db2 = CreateDb();

        // Old readings should be deleted, recent + newly polled should remain
        var readings = await db2.DeviceReadings.ToListAsync();
        Assert.DoesNotContain(readings, r => r.Timestamp < DateTime.UtcNow.AddDays(-30));
        Assert.Contains(readings, r => r.Timestamp > DateTime.UtcNow.AddDays(-2));

        // Old polling log should be deleted, recent + new should remain
        var logs = await db2.PollingLogs.ToListAsync();
        Assert.DoesNotContain(logs, l => l.Timestamp < DateTime.UtcNow.AddDays(-30));
        Assert.Contains(logs, l => l.Timestamp > DateTime.UtcNow.AddDays(-2));
    }
}
