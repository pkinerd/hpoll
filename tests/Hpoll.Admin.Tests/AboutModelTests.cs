using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Hpoll.Admin.Pages;
using Hpoll.Core.Configuration;
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
    public async Task OnGetAsync_ReturnsVersionAndRuntimeInfo()
    {
        var hueApp = Options.Create(new HueAppSettings { ClientId = "test-id", CallbackUrl = "http://localhost/callback" });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DataPath"] = "/tmp/test"
        }).Build();

        var model = new AboutModel(_db, hueApp, config);
        await model.OnGetAsync();

        Assert.NotEmpty(model.Version);
        Assert.StartsWith(".NET", model.Runtime);
        Assert.True(model.HueAppConfigured);
        Assert.Equal("http://localhost/callback", model.HueCallbackUrl);
    }

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

        var hueApp = Options.Create(new HueAppSettings());
        var config = new ConfigurationBuilder().Build();
        var model = new AboutModel(_db, hueApp, config);
        await model.OnGetAsync();

        Assert.Equal(1, model.CustomerCount);
        Assert.Equal(1, model.HubCount);
        Assert.Equal(1, model.DeviceCount);
        Assert.False(model.HueAppConfigured);
    }
}
