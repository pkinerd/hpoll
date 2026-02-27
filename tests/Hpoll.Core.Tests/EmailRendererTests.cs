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
    public async Task RenderDailySummaryAsync_WithNoReadings_ReturnsHtml()
    {
        var (customer, _, _) = await SeedBaseDataAsync();
        var date = new DateTime(2026, 2, 27, 0, 0, 0, DateTimeKind.Utc);

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, date);

        Assert.NotNull(html);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("Daily Activity Summary", html);
        Assert.Contains("No data", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithMotionReadings_ShowsActivity()
    {
        var (customer, _, device) = await SeedBaseDataAsync();
        var date = new DateTime(2026, 2, 27, 0, 0, 0, DateTimeKind.Utc);

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = date.AddHours(10),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = date.AddHours(11),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T11:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, date);

        Assert.Contains("Motion Activity", html);
        Assert.Contains("Location Diversity", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithTemperatureReadings_ShowsRange()
    {
        var (customer, _, device) = await SeedBaseDataAsync();
        var date = new DateTime(2026, 2, 27, 0, 0, 0, DateTimeKind.Utc);

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = date.AddHours(10),
            ReadingType = "temperature",
            Value = "{\"temperature\":19.5,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = date.AddHours(11),
            ReadingType = "temperature",
            Value = "{\"temperature\":22.0,\"changed\":\"2026-02-27T11:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, date);

        Assert.Contains("Temperature Range", html);
        Assert.Contains("19.5", html);
        Assert.Contains("22.0", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_ContainsPrivacyFooter()
    {
        var (customer, _, _) = await SeedBaseDataAsync();
        var date = new DateTime(2026, 2, 27, 0, 0, 0, DateTimeKind.Utc);

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, date);

        Assert.Contains("No individual device or location details are included for privacy", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_ContainsAllSixWindows()
    {
        var (customer, _, _) = await SeedBaseDataAsync();
        var date = new DateTime(2026, 2, 27, 0, 0, 0, DateTimeKind.Utc);

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, date);

        Assert.Contains("00:00", html);
        Assert.Contains("04:00", html);
        Assert.Contains("08:00", html);
        Assert.Contains("12:00", html);
        Assert.Contains("16:00", html);
        Assert.Contains("20:00", html);
    }
}
