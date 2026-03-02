using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Hpoll.Core.Constants;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Tests.Integration;

public class DashboardPageTests : IClassFixture<HpollWebApplicationFactory>, IAsyncLifetime, IDisposable
{
    private readonly HpollWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DashboardPageTests(HpollWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Dashboard_ReturnsSuccessAndContainsTitle()
    {
        var response = await _client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Dashboard", html);
    }

    [Fact]
    public async Task Dashboard_ShowsNavigationBar()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("hpoll", html);
        Assert.Contains("Dashboard", html);
        Assert.Contains("Customers", html);
        Assert.Contains("About", html);
        Assert.Contains("Logout", html);
    }

    [Fact]
    public async Task Dashboard_ShowsCustomerAndHubCounts()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Dashboard Test",
            Email = "dash@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "DASH001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        // Should show the customer and hub cards
        Assert.Contains("Customers", html);
        Assert.Contains("Hubs Active", html);
    }

    [Fact]
    public async Task Dashboard_ShowsExpiringTokenWarning()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Expiring Token Customer",
            Email = "expire@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "EXPIRE001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddHours(12), // Expires within 48h
            Status = HubStatus.Active
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Token Expiry Warnings", html);
        Assert.Contains("Expiring Token Customer", html);
        Assert.Contains("EXPIRE001", html);
    }

    [Fact]
    public async Task Dashboard_ShowsFailingHubs()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Failing Hub Customer",
            Email = "fail@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "FAIL001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active,
            ConsecutiveFailures = 5
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Hubs with Failures", html);
        Assert.Contains("Failing Hub Customer", html);
        Assert.Contains("FAIL001", html);
    }

    [Fact]
    public async Task Dashboard_ShowsRecentPollingActivity()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Poll Log Customer",
            Email = "poll@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "POLL001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        db.PollingLogs.Add(new PollingLog
        {
            HubId = hub.Id,
            Success = true,
            ApiCallsMade = 3,
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Recent Polling Activity", html);
        Assert.Contains("POLL001", html);
        Assert.Contains("OK", html);
    }

    [Fact]
    public async Task Dashboard_ShowsNoPollActivityMessage_WhenEmpty()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Recent Polling Activity", html);
        Assert.Contains("No polling activity yet", html);
    }

    public void Dispose() => _client.Dispose();
}
