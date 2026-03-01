using Microsoft.EntityFrameworkCore;
using Hpoll.Admin.Pages;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Tests;

public class DashboardModelTests : IDisposable
{
    private readonly HpollDbContext _db;

    public DashboardModelTests()
    {
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new HpollDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Customer> SeedCustomerAsync(string status = "active")
    {
        var customer = new Customer { Name = "Test User", Email = $"{Guid.NewGuid()}@test.com", TimeZoneId = "UTC", Status = status };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return customer;
    }

    private async Task<Hub> SeedHubAsync(int customerId, string status = "active", DateTime? tokenExpiresAt = null, int consecutiveFailures = 0)
    {
        var hub = new Hub
        {
            CustomerId = customerId,
            HueBridgeId = Guid.NewGuid().ToString("N")[..16],
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = tokenExpiresAt ?? DateTime.UtcNow.AddDays(7),
            Status = status,
            ConsecutiveFailures = consecutiveFailures
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();
        return hub;
    }

    [Fact]
    public async Task OnGetAsync_ReturnsCorrectCounts()
    {
        var c1 = await SeedCustomerAsync("active");
        var c2 = await SeedCustomerAsync("active");
        var c3 = await SeedCustomerAsync("inactive");

        await SeedHubAsync(c1.Id, "active");
        await SeedHubAsync(c1.Id, "active");
        await SeedHubAsync(c2.Id, "inactive");
        await SeedHubAsync(c3.Id, "needs_reauth");

        var model = new IndexModel(_db);
        await model.OnGetAsync();

        Assert.Equal(2, model.ActiveCustomers);
        Assert.Equal(1, model.InactiveCustomers);
        Assert.Equal(2, model.ActiveHubs);
        Assert.Equal(1, model.InactiveHubs);
        Assert.Equal(1, model.NeedsReauthHubs);
    }

    [Fact]
    public async Task OnGetAsync_ShowsExpiringTokens()
    {
        var customer = await SeedCustomerAsync();
        // Expires in 24 hours (within 48h threshold)
        await SeedHubAsync(customer.Id, "active", DateTime.UtcNow.AddHours(24));
        // Expires in 72 hours (outside 48h threshold)
        await SeedHubAsync(customer.Id, "active", DateTime.UtcNow.AddHours(72));

        var model = new IndexModel(_db);
        await model.OnGetAsync();

        Assert.Single(model.ExpiringTokenHubs);
    }

    [Fact]
    public async Task OnGetAsync_ShowsFailingHubs()
    {
        var customer = await SeedCustomerAsync();
        await SeedHubAsync(customer.Id, "active", consecutiveFailures: 3);
        await SeedHubAsync(customer.Id, "active", consecutiveFailures: 0);

        var model = new IndexModel(_db);
        await model.OnGetAsync();

        Assert.Single(model.FailingHubs);
        Assert.Equal(3, model.FailingHubs[0].ConsecutiveFailures);
    }

    [Fact]
    public async Task OnGetAsync_EmptyDatabase_ReturnsZeroCounts()
    {
        var model = new IndexModel(_db);
        await model.OnGetAsync();

        Assert.Equal(0, model.ActiveCustomers);
        Assert.Equal(0, model.InactiveCustomers);
        Assert.Equal(0, model.ActiveHubs);
        Assert.Equal(0, model.InactiveHubs);
        Assert.Equal(0, model.NeedsReauthHubs);
        Assert.Empty(model.ExpiringTokenHubs);
        Assert.Empty(model.FailingHubs);
        Assert.Empty(model.RecentLogs);
    }
}
