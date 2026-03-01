using Microsoft.EntityFrameworkCore;
using Hpoll.Admin.Pages;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Tests;

public class AboutModelTests : IDisposable
{
    private readonly HpollDbContext _db;

    public AboutModelTests()
    {
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new HpollDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task OnGetAsync_ReturnsCorrectDbCounts()
    {
        var customer = new Customer { Name = "Test", Email = "test@example.com", TimeZoneId = "UTC" };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "001788FFFE123456",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = "active"
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        var device = new Device { HubId = hub.Id, HueDeviceId = "dev-001", DeviceType = "motion_sensor", Name = "Sensor" };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        var model = new AboutModel(_db);
        await model.OnGetAsync();

        Assert.Equal(1, model.CustomerCount);
        Assert.Equal(1, model.HubCount);
        Assert.Equal(1, model.DeviceCount);
    }

    [Fact]
    public async Task OnGetAsync_GroupsSystemInfoByCategory()
    {
        _db.SystemInfo.AddRange(
            new SystemInfo { Key = "system.version", Value = "1.0.0", Category = "System" },
            new SystemInfo { Key = "polling.interval_minutes", Value = "60", Category = "Polling" },
            new SystemInfo { Key = "email.aws_region", Value = "us-east-1", Category = "Email" }
        );
        await _db.SaveChangesAsync();

        var model = new AboutModel(_db);
        await model.OnGetAsync();

        Assert.Equal(3, model.Sections.Count);
        Assert.Equal("System", model.Sections[0].Category);
        Assert.Equal("Polling", model.Sections[1].Category);
        Assert.Equal("Email", model.Sections[2].Category);
    }

    [Fact]
    public async Task OnGetAsync_FormatsLabelsCorrectly()
    {
        _db.SystemInfo.Add(new SystemInfo { Key = "polling.interval_minutes", Value = "60", Category = "Polling" });
        await _db.SaveChangesAsync();

        var model = new AboutModel(_db);
        await model.OnGetAsync();

        var entry = model.Sections[0].Entries[0];
        Assert.Equal("Interval Minutes", entry.Label);
    }

    [Fact]
    public async Task OnGetAsync_FormatsDateValues()
    {
        _db.SystemInfo.Add(new SystemInfo { Key = "runtime.last_poll_completed", Value = "2026-03-01T10:00:00.0000000Z", Category = "Runtime" });
        await _db.SaveChangesAsync();

        var model = new AboutModel(_db);
        await model.OnGetAsync();

        var entry = model.Sections[0].Entries[0];
        Assert.Contains("2026-03-01 10:00:00", entry.Value);
        Assert.Contains("UTC", entry.Value);
    }

    [Fact]
    public async Task OnGetAsync_HandlesEmptySystemInfoTable()
    {
        var model = new AboutModel(_db);
        await model.OnGetAsync();

        Assert.Empty(model.Sections);
        Assert.Equal(0, model.CustomerCount);
    }

    [Fact]
    public async Task OnGetAsync_SectionsInExpectedOrder()
    {
        _db.SystemInfo.AddRange(
            new SystemInfo { Key = "runtime.total_poll_cycles", Value = "5", Category = "Runtime" },
            new SystemInfo { Key = "email.aws_region", Value = "us-east-1", Category = "Email" },
            new SystemInfo { Key = "system.version", Value = "1.0.0", Category = "System" },
            new SystemInfo { Key = "polling.interval_minutes", Value = "60", Category = "Polling" },
            new SystemInfo { Key = "hue.app_configured", Value = "True", Category = "Hue" }
        );
        await _db.SaveChangesAsync();

        var model = new AboutModel(_db);
        await model.OnGetAsync();

        var categories = model.Sections.Select(s => s.Category).ToList();
        Assert.Equal(new[] { "System", "Polling", "Email", "Hue", "Runtime" }, categories);
    }
}
