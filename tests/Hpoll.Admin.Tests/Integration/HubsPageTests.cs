using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Hpoll.Core.Constants;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Tests.Integration;

public class HubsPageTests : IClassFixture<HpollWebApplicationFactory>, IDisposable
{
    private readonly HpollWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HubsPageTests(HpollWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task HubDetail_ReturnsSuccessForExistingHub()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Hub Detail Customer",
            Email = "hubdetail@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "HUBDET001",
            HueApplicationKey = "appkey123",
            AccessToken = "access123",
            RefreshToken = "refresh123",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Hubs/Detail/{hub.Id}");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("HUBDET001", html);
    }

    [Fact]
    public async Task HubDetail_ShowsHubStatusAndInfo()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Hub Info Customer",
            Email = "hubinfo@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "HUBINF001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active,
            ConsecutiveFailures = 0
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Hubs/Detail/{hub.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Status", html);
        Assert.Contains("status-active", html);
        Assert.Contains("Token Expires", html);
        Assert.Contains("Last Polled", html);
        Assert.Contains("Consecutive Failures", html);
    }

    [Fact]
    public async Task HubDetail_ShowsCustomerLink()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Linked Customer",
            Email = "link@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "LINK001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Hubs/Detail/{hub.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Linked Customer", html);
        Assert.Contains($"/Customers/Detail/{customer.Id}", html);
    }

    [Fact]
    public async Task HubDetail_ShowsDeactivateButton_ForActiveHub()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Active Hub Customer",
            Email = "activehub@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "ACTIVE001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Hubs/Detail/{hub.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Deactivate", html);
    }

    [Fact]
    public async Task HubDetail_ShowsReactivateButton_ForInactiveHub()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Inactive Hub Customer",
            Email = "inactivehub@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "INACT001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Inactive,
            DeactivatedAt = DateTime.UtcNow
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Hubs/Detail/{hub.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Reactivate", html);
    }

    [Fact]
    public async Task HubDetail_ShowsClearReauthButton_ForNeedsReauthStatus()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Reauth Hub Customer",
            Email = "reauth@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "REAUTH001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.NeedsReauth
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Hubs/Detail/{hub.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Clear Needs Re-auth", html);
        Assert.Contains("status-needs_reauth", html);
    }

    [Fact]
    public async Task HubDetail_ShowsTokenSection()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Token Section Customer",
            Email = "tokensection@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "TOKEN001",
            HueApplicationKey = "key",
            AccessToken = "someaccesstoken",
            RefreshToken = "somerefreshtoken",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Hubs/Detail/{hub.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Tokens", html);
        Assert.Contains("Application Key", html);
        Assert.Contains("Access Token", html);
        Assert.Contains("Refresh Token", html);
        // Tokens should be masked by default (bullet character HTML-encoded)
        Assert.Contains("&#x2022;", html);
    }

    [Fact]
    public async Task HubDetail_ShowsActionButtons()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Actions Customer",
            Email = "actions@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "ACT001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Hubs/Detail/{hub.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Refresh Token Now", html);
        Assert.Contains("Test Connection", html);
        Assert.Contains("Delete Hub", html);
    }

    [Fact]
    public async Task HubDetail_ShowsDevicesSection()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Devices Section Customer",
            Email = "devices@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "DEVSEC001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        db.Devices.Add(new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-001",
            DeviceType = "motion_sensor",
            Name = "Living Room Motion",
            RoomName = "Living Room"
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Hubs/Detail/{hub.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Devices", html);
        Assert.Contains("Living Room Motion", html);
        Assert.Contains("motion_sensor", html);
        Assert.Contains("Living Room", html);
    }

    [Fact]
    public async Task HubDetail_ShowsNoDevicesMessage_WhenEmpty()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "No Devices Customer",
            Email = "nodevices@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "NODEV001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Hubs/Detail/{hub.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("No devices discovered yet", html);
    }

    [Fact]
    public async Task HubDetail_ShowsPollingLogsSection()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Polling Logs Customer",
            Email = "pollinglogs@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "POLLLOG001",
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
            Timestamp = DateTime.UtcNow,
            Success = true,
            ApiCallsMade = 5
        });
        db.PollingLogs.Add(new PollingLog
        {
            HubId = hub.Id,
            Timestamp = DateTime.UtcNow.AddMinutes(-30),
            Success = false,
            ErrorMessage = "Connection timeout",
            ApiCallsMade = 1
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Hubs/Detail/{hub.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Recent Polling Logs", html);
        Assert.Contains("OK", html);
        Assert.Contains("FAIL", html);
        Assert.Contains("Connection timeout", html);
    }

    [Fact]
    public async Task HubDetail_ShowsTokenExpiryColor_Red_ForSoonExpiring()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Red Token Customer",
            Email = "redtoken@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "REDTOK001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddHours(6), // < 24h = red
            Status = HubStatus.Active
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Hubs/Detail/{hub.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("token-red", html);
    }

    [Fact]
    public async Task HubDetail_ShowsTokenExpiryColor_Green_ForGoodExpiry()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Green Token Customer",
            Email = "greentoken@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "GRNTOK001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7), // > 48h = green
            Status = HubStatus.Active
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Hubs/Detail/{hub.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("token-green", html);
    }

    [Fact]
    public async Task HubDetail_Returns404_ForNonExistentHub()
    {
        var response = await _client.GetAsync("/Hubs/Detail/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HubDetail_ShowsDeleteButton_Disabled_ForRecentlyDeactivatedHub()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Delete Button Customer",
            Email = "delete@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "DELBTN001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Inactive,
            DeactivatedAt = DateTime.UtcNow // Just now = can't delete yet
        };
        db.Hubs.Add(hub);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Hubs/Detail/{hub.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Delete Hub", html);
        Assert.Contains("disabled", html);
        Assert.Contains("Available in", html);
    }

    public void Dispose() => _client.Dispose();
}
