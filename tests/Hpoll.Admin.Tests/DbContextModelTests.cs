using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Tests;

public class DbContextModelTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly HpollDbContext _db;

    public DbContextModelTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new HpollDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<Customer> SeedCustomerAsync(string email = "test@example.com")
    {
        var customer = new Customer { Name = "Test User", Email = email, TimeZoneId = "UTC" };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return customer;
    }

    [Fact]
    public async Task Customer_DuplicateEmail_ThrowsUniqueConstraintViolation()
    {
        await SeedCustomerAsync("duplicate@example.com");

        _db.Customers.Add(new Customer { Name = "Another", Email = "duplicate@example.com", TimeZoneId = "UTC" });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Hub_DuplicateBridgeId_ThrowsUniqueConstraintViolation()
    {
        var customer = await SeedCustomerAsync();
        _db.Hubs.Add(new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "DUPLICATE_BRIDGE",
            HueApplicationKey = "key1",
            AccessToken = "token1",
            RefreshToken = "refresh1",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = "active"
        });
        await _db.SaveChangesAsync();

        _db.Hubs.Add(new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "DUPLICATE_BRIDGE",
            HueApplicationKey = "key2",
            AccessToken = "token2",
            RefreshToken = "refresh2",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = "active"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Device_DuplicateHubIdAndHueDeviceId_ThrowsUniqueConstraintViolation()
    {
        var customer = await SeedCustomerAsync();
        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "BRIDGE001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = "active"
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        _db.Devices.Add(new Device { HubId = hub.Id, HueDeviceId = "device-001", DeviceType = "motion_sensor", Name = "Sensor A" });
        await _db.SaveChangesAsync();

        _db.Devices.Add(new Device { HubId = hub.Id, HueDeviceId = "device-001", DeviceType = "motion_sensor", Name = "Sensor B" });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task CascadeDelete_DeleteCustomer_DeletesHubsDevicesReadings()
    {
        var customer = await SeedCustomerAsync("cascade@example.com");
        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "CASCADE001",
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

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = DateTime.UtcNow,
            ReadingType = "motion",
            Value = "{\"motion\":true}"
        });
        await _db.SaveChangesAsync();

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();

        Assert.Equal(0, await _db.Hubs.CountAsync());
        Assert.Equal(0, await _db.Devices.CountAsync());
        Assert.Equal(0, await _db.DeviceReadings.CountAsync());
    }

    [Fact]
    public async Task CascadeDelete_DeleteHub_DeletesDevicesAndReadings()
    {
        var customer = await SeedCustomerAsync("cascade2@example.com");
        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "CASCADE002",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = "active"
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        var device = new Device { HubId = hub.Id, HueDeviceId = "dev-002", DeviceType = "motion_sensor", Name = "Sensor" };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = DateTime.UtcNow,
            ReadingType = "temperature",
            Value = "{\"temperature\":22.0}"
        });
        _db.PollingLogs.Add(new PollingLog
        {
            HubId = hub.Id,
            Timestamp = DateTime.UtcNow,
            Success = true,
            ApiCallsMade = 3
        });
        await _db.SaveChangesAsync();

        _db.Hubs.Remove(hub);
        await _db.SaveChangesAsync();

        // Customer should remain
        Assert.Equal(1, await _db.Customers.CountAsync());
        Assert.Equal(0, await _db.Devices.CountAsync());
        Assert.Equal(0, await _db.DeviceReadings.CountAsync());
        Assert.Equal(0, await _db.PollingLogs.CountAsync());
    }
}
