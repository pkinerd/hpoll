using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Hpoll.Data;
using Hpoll.Data.Entities;
using Hpoll.Email;

namespace Hpoll.Core.Tests;

public class EmailRendererTests : IDisposable
{
    private readonly HpollDbContext _db;
    private readonly EmailRenderer _renderer;

    // endUtc at midnight means the 24h window is [Feb 27 00:00, Feb 28 00:00)
    private static readonly DateTime EndUtc = new(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc);

    public EmailRendererTests()
    {
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new HpollDbContext(options);
        _renderer = new EmailRenderer(_db, NullLogger<EmailRenderer>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<(Customer customer, Hub hub, Device device)> SeedBaseDataAsync()
    {
        var customer = new Customer { Name = "Test User", Email = "test@example.com" };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "001788FFFE123456",
            HueApplicationKey = "testkey",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = "active"
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        var device = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-001",
            DeviceType = "motion_sensor",
            Name = "Sensor 1"
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        return (customer, hub, device);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithNoReadings_ReturnsNull()
    {
        var (customer, _, _) = await SeedBaseDataAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, EndUtc);

        Assert.Null(html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithMotionReadings_ShowsActivity()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // Readings at Feb 27 10:00 and 11:00 — within [Feb 27 00:00, Feb 28 00:00)
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 11, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T11:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, EndUtc);

        Assert.NotNull(html);
        Assert.Contains("Motion Activity", html);
        Assert.Contains("Location Diversity", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithTemperatureReadings_ShowsRange()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "temperature",
            Value = "{\"temperature\":19.5,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 11, 0, 0, DateTimeKind.Utc),
            ReadingType = "temperature",
            Value = "{\"temperature\":22.0,\"changed\":\"2026-02-27T11:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, EndUtc);

        Assert.NotNull(html);
        Assert.Contains("Temperature Range", html);
        Assert.Contains("19.5", html);
        Assert.Contains("22.0", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_ContainsPrivacyFooter()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, EndUtc);

        Assert.NotNull(html);
        Assert.Contains("No individual device or location details are included for privacy", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_ContainsAllSixWindows()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, EndUtc);

        Assert.NotNull(html);
        // Six 4-hour windows starting from startUtc (Feb 27 00:00)
        Assert.Contains("00:00", html);
        Assert.Contains("04:00", html);
        Assert.Contains("08:00", html);
        Assert.Contains("12:00", html);
        Assert.Contains("16:00", html);
        Assert.Contains("20:00", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithMalformedJson_HandlesGracefully()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "not-valid-json"
        });
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "temperature",
            Value = "{invalid}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, EndUtc);

        Assert.NotNull(html);
        Assert.Contains("Daily Activity Summary", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithMotionFalse_DoesNotCountAsEvent()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "{\"motion\":false,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, EndUtc);

        Assert.NotNull(html);
        Assert.Contains("Daily Activity Summary", html);
        // 0 areas active out of 1 motion sensor
        Assert.Contains("0/1", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithNoCustomerHubs_ReturnsNull()
    {
        var customer = new Customer { Name = "No Hubs User", Email = "nohubs@example.com" };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, EndUtc);

        Assert.Null(html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithMultipleDevices_ShowsCorrectDiversity()
    {
        var (customer, hub, device1) = await SeedBaseDataAsync();

        var device2 = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-002",
            DeviceType = "motion_sensor",
            Name = "Sensor 2"
        };
        _db.Devices.Add(device2);
        await _db.SaveChangesAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device1.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, EndUtc);

        Assert.NotNull(html);
        // 1 out of 2 motion sensors had activity
        Assert.Contains("1/2", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_HeaderShowsTimeRange()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, EndUtc);

        Assert.NotNull(html);
        // Header shows the 24h range: "27 Feb 2026 00:00 – 28 Feb 2026 00:00 UTC"
        Assert.Contains("27 Feb 2026", html);
        Assert.Contains("28 Feb 2026", html);
        Assert.Contains("UTC", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_ReadingsOutsideWindow_AreExcluded()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // Reading inside the window
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        // Reading outside the window (before startUtc)
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 26, 23, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-26T23:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, EndUtc);

        Assert.NotNull(html);
        Assert.Contains("Motion Activity", html);
    }
}
