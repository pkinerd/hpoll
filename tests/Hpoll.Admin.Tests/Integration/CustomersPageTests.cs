using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Hpoll.Core.Constants;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Tests.Integration;

public class CustomersPageTests : IClassFixture<HpollWebApplicationFactory>, IAsyncLifetime, IDisposable
{
    private readonly HpollWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CustomersPageTests(HpollWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // --- Customers/Index ---

    [Fact]
    public async Task CustomersIndex_ReturnsSuccessAndShowsTitle()
    {
        var response = await _client.GetAsync("/Customers");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Customers", html);
    }

    [Fact]
    public async Task CustomersIndex_ShowsCustomerTable()
    {
        using var db = _factory.CreateDbContext();
        db.Customers.Add(new Customer
        {
            Name = "IndexTest Customer",
            Email = "index@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/Customers");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("IndexTest Customer", html);
        Assert.Contains("index@test.com", html);
        Assert.Contains("<table>", html);
    }

    [Fact]
    public async Task CustomersIndex_ShowsAddCustomerButton()
    {
        var response = await _client.GetAsync("/Customers");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Add Customer", html);
    }

    [Fact]
    public async Task CustomersIndex_ShowsNoCustomersMessage_WhenEmpty()
    {
        // Database is reset before each test via IAsyncLifetime, so no customers exist
        var response = await _client.GetAsync("/Customers");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("No customers yet", html);
    }

    [Fact]
    public async Task CustomersIndex_ShowsStatusBadge()
    {
        using var db = _factory.CreateDbContext();
        db.Customers.Add(new Customer
        {
            Name = "Status Badge Customer",
            Email = "badge@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/Customers");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("status-active", html);
    }

    // --- Customers/Create ---

    [Fact]
    public async Task CustomersCreate_ReturnsSuccessAndShowsForm()
    {
        var response = await _client.GetAsync("/Customers/Create");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Add Customer", html);
        Assert.Contains("<form", html);
        Assert.Contains("Name", html);
        Assert.Contains("Notification Email", html);
        Assert.Contains("Timezone", html);
        Assert.Contains("Email Send Times", html);
    }

    [Fact]
    public async Task CustomersCreate_HasTimezoneDropdown()
    {
        var response = await _client.GetAsync("/Customers/Create");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<select", html);
        Assert.Contains("Australia/Sydney", html);
        Assert.Contains("UTC", html);
    }

    [Fact]
    public async Task CustomersCreate_HasCancelButton()
    {
        var response = await _client.GetAsync("/Customers/Create");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Cancel", html);
    }

    // --- Customers/Detail ---

    [Fact]
    public async Task CustomersDetail_ReturnsSuccessForExistingCustomer()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Detail Page Customer",
            Email = "detail@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active,
            SendTimesLocal = "19:30"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Customers/Detail/{customer.Id}");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Detail Page Customer", html);
    }

    [Fact]
    public async Task CustomersDetail_ShowsCustomerInfo()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Info Display Customer",
            Email = "info@test.com",
            TimeZoneId = "Australia/Sydney",
            Status = CustomerStatus.Active,
            SendTimesLocal = "08:00, 19:30"
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Customers/Detail/{customer.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Info Display Customer", html);
        Assert.Contains("Status", html);
        Assert.Contains("status-active", html);
        Assert.Contains("Timezone", html);
        Assert.Contains("Email Send Times", html);
        Assert.Contains("08:00, 19:30", html);
    }

    [Fact]
    public async Task CustomersDetail_ShowsEditForms()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Edit Forms Customer",
            Email = "edit@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Customers/Detail/{customer.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Update Name", html);
        Assert.Contains("Change Timezone", html);
        Assert.Contains("Update Emails", html);
        Assert.Contains("Update Send Times", html);
    }

    [Fact]
    public async Task CustomersDetail_ShowsDeactivateButton_ForActiveCustomer()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Active Customer",
            Email = "active@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Customers/Detail/{customer.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Deactivate", html);
    }

    [Fact]
    public async Task CustomersDetail_ShowsReactivateButton_ForInactiveCustomer()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Inactive Customer",
            Email = "inactive@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Inactive
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Customers/Detail/{customer.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Reactivate", html);
    }

    [Fact]
    public async Task CustomersDetail_ShowsRegisterHubButton()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Hub Register Customer",
            Email = "hub@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Customers/Detail/{customer.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Register Hub", html);
    }

    [Fact]
    public async Task CustomersDetail_ShowsHubsTable_WhenCustomerHasHubs()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Hub Table Customer",
            Email = "hubtable@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.Hubs.Add(new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "CUSTDET001",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Customers/Detail/{customer.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("CUSTDET001", html);
        Assert.Contains("Bridge ID", html);
    }

    [Fact]
    public async Task CustomersDetail_ShowsNoHubsMessage_WhenEmpty()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "No Hubs Customer",
            Email = "nohubs@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Customers/Detail/{customer.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("No hubs registered", html);
    }

    [Fact]
    public async Task CustomersDetail_Returns404_ForNonExistentCustomer()
    {
        var response = await _client.GetAsync("/Customers/Detail/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CustomersDetail_ShowsActivitySummaryToggle()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "Activity Customer",
            Email = "activity@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Customers/Detail/{customer.Id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Activity Summary", html);
        Assert.Contains("Show Activity Summary", html);
    }

    [Fact]
    public async Task CustomersDetail_TimezoneReadOnly_ByDefault()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "TZ Readonly Customer",
            Email = "tzreadonly@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Customers/Detail/{customer.Id}");
        var html = await response.Content.ReadAsStringAsync();

        // Should show read-only input and "Change Timezone" link, not a <select> dropdown
        Assert.Contains("Change Timezone", html);
        Assert.Contains("readonly", html);
        Assert.DoesNotContain("Update Timezone", html);
    }

    [Fact]
    public async Task CustomersDetail_TimezoneDropdown_ShownWhenEditTzTrue()
    {
        using var db = _factory.CreateDbContext();
        var customer = new Customer
        {
            Name = "TZ Edit Customer",
            Email = "tzedit@test.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/Customers/Detail/{customer.Id}?editTz=true");
        var html = await response.Content.ReadAsStringAsync();

        // Should show the <select> dropdown and Update/Cancel buttons
        Assert.Contains("<select", html);
        Assert.Contains("Update Timezone", html);
        Assert.Contains("Cancel", html);
        // Should NOT show the read-only "Change Timezone" link
        Assert.DoesNotContain("Change Timezone", html);
    }

    public void Dispose() => _client.Dispose();
}
