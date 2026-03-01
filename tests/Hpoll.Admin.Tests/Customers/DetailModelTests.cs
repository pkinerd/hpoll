using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Hpoll.Admin.Pages.Customers;
using Hpoll.Core.Configuration;
using Hpoll.Core.Constants;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Tests.Customers;

public class DetailModelTests : IDisposable
{
    private readonly HpollDbContext _db;

    public DetailModelTests()
    {
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new HpollDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private DetailModel CreatePageModel()
    {
        var hueApp = Options.Create(new HueAppSettings());
        var emailSettings = Options.Create(new EmailSettings());
        var model = new DetailModel(_db, hueApp, emailSettings, NullLogger<DetailModel>.Instance);
        model.PageContext = new PageContext
        {
            ActionDescriptor = new CompiledPageActionDescriptor(),
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData()
        };
        return model;
    }

    private async Task<Customer> SeedCustomerAsync(string name = "Test User", string email = "test@example.com")
    {
        var customer = new Customer { Name = name, Email = email, TimeZoneId = "UTC", Status = CustomerStatus.Active };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return customer;
    }

    private async Task<Hub> SeedHubAsync(int customerId)
    {
        var hub = new Hub
        {
            CustomerId = customerId,
            HueBridgeId = Guid.NewGuid().ToString("N")[..16],
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();
        return hub;
    }

    [Fact]
    public async Task OnGetAsync_ValidCustomer_LoadsCustomerWithHubs()
    {
        var customer = await SeedCustomerAsync();
        var hub = await SeedHubAsync(customer.Id);

        var model = CreatePageModel();
        var result = await model.OnGetAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal(customer.Id, model.Customer.Id);
        Assert.Single(model.Customer.Hubs);
    }

    [Fact]
    public async Task OnGetAsync_InvalidCustomer_ReturnsNotFound()
    {
        var model = CreatePageModel();
        var result = await model.OnGetAsync(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostUpdateNameAsync_ValidData_UpdatesCustomer()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Updated Name";

        var result = await model.OnPostUpdateNameAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Name updated.", model.SuccessMessage);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal("Updated Name", updated!.Name);
    }

    [Fact]
    public async Task OnPostUpdateNameAsync_EmptyName_ReturnsError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "";

        var result = await model.OnPostUpdateNameAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditName"));
    }

    [Fact]
    public async Task OnPostToggleStatusAsync_ActiveToInactive_TogglesStatus()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        var result = await model.OnPostToggleStatusAsync(customer.Id);

        Assert.IsType<RedirectToPageResult>(result);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal(CustomerStatus.Inactive, updated!.Status);
    }

    [Fact]
    public async Task OnPostToggleStatusAsync_InactiveToActive_TogglesBack()
    {
        var customer = await SeedCustomerAsync();
        customer.Status = CustomerStatus.Inactive;
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        var result = await model.OnPostToggleStatusAsync(customer.Id);

        Assert.IsType<RedirectToPageResult>(result);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal(CustomerStatus.Active, updated!.Status);
    }

    [Fact]
    public async Task OnPostUpdateEmailsAsync_ValidData_UpdatesAllEmailFields()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditEmail = "updated@example.com";
        model.EditCcEmails = "cc@example.com";
        model.EditBccEmails = "bcc@example.com";

        var result = await model.OnPostUpdateEmailsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Email addresses updated.", model.SuccessMessage);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal("updated@example.com", updated!.Email);
        Assert.Equal("cc@example.com", updated.CcEmails);
        Assert.Equal("bcc@example.com", updated.BccEmails);
    }

    [Fact]
    public async Task OnPostUpdateNameAsync_InvalidCustomer_ReturnsNotFound()
    {
        var model = CreatePageModel();
        model.EditName = "Name";

        var result = await model.OnPostUpdateNameAsync(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_WithActivity_ShowsActivitySummary()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        var result = await model.OnGetAsync(customer.Id, activity: true);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ShowActivitySummary);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesSendTimesLocal()
    {
        var customer = await SeedCustomerAsync();
        customer.SendTimesLocal = "19:30";
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.Equal("19:30", model.EditSendTimesLocal);
    }

    [Fact]
    public async Task OnPostUpdateSendTimesAsync_ValidTimes_UpdatesCustomer()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditSendTimesLocal = "07:00, 19:30";

        var result = await model.OnPostUpdateSendTimesAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Contains("Send times updated", model.SuccessMessage);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal("07:00, 19:30", updated!.SendTimesLocal);
        Assert.NotNull(updated.NextSendTimeUtc);
    }

    [Fact]
    public async Task OnPostUpdateSendTimesAsync_EmptyTimes_ClearsToDefault()
    {
        var customer = await SeedCustomerAsync();
        customer.SendTimesLocal = "19:30";
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        model.EditSendTimesLocal = "";

        var result = await model.OnPostUpdateSendTimesAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal("", updated!.SendTimesLocal);
        Assert.NotNull(updated.NextSendTimeUtc); // Falls back to default
    }

    [Fact]
    public async Task OnPostUpdateSendTimesAsync_InvalidTimes_ReturnsError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditSendTimesLocal = "invalid";

        var result = await model.OnPostUpdateSendTimesAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditSendTimesLocal"));
    }

    [Fact]
    public async Task OnPostUpdateSendTimesAsync_InvalidCustomer_ReturnsNotFound()
    {
        var model = CreatePageModel();
        model.EditSendTimesLocal = "19:30";

        var result = await model.OnPostUpdateSendTimesAsync(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostUpdateTimeZoneAsync_ValidTimezone_UpdatesCustomer()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditTimeZoneId = "America/New_York";

        var result = await model.OnPostUpdateTimeZoneAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Timezone updated.", model.SuccessMessage);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal("America/New_York", updated!.TimeZoneId);
    }

    [Fact]
    public async Task OnPostUpdateTimeZoneAsync_InvalidTimezone_ReturnsError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditTimeZoneId = "Invalid/Timezone";

        var result = await model.OnPostUpdateTimeZoneAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditTimeZoneId"));
    }

    [Fact]
    public async Task OnPostUpdateTimeZoneAsync_InvalidCustomer_ReturnsNotFound()
    {
        var model = CreatePageModel();
        model.EditTimeZoneId = "UTC";

        var result = await model.OnPostUpdateTimeZoneAsync(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostUpdateTimeZoneAsync_RecomputesNextSendTime()
    {
        var customer = await SeedCustomerAsync();
        customer.SendTimesLocal = "19:30";
        customer.NextSendTimeUtc = DateTime.UtcNow.AddDays(1);
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        model.EditTimeZoneId = "America/New_York";

        await model.OnPostUpdateTimeZoneAsync(customer.Id);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.NotNull(updated!.NextSendTimeUtc);
    }

    [Fact]
    public async Task OnGetAsync_DefaultSendTimesFromSystemInfo_WhenAvailable()
    {
        var customer = await SeedCustomerAsync();
        _db.SystemInfo.Add(new SystemInfo
        {
            Key = "email.send_times_utc",
            Value = "08:00, 20:00",
            Category = "Email"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.Equal("08:00, 20:00 UTC", model.DefaultSendTimesDisplay);
    }

    [Fact]
    public async Task OnGetAsync_DefaultSendTimesFallback_WhenNoSystemInfo()
    {
        var customer = await SeedCustomerAsync();

        var emailSettings = Options.Create(new EmailSettings { SendTimesUtc = new List<string> { "09:00" } });
        var model = new DetailModel(_db, Options.Create(new HueAppSettings()), emailSettings, NullLogger<DetailModel>.Instance);
        model.PageContext = new PageContext
        {
            ActionDescriptor = new CompiledPageActionDescriptor(),
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData()
        };

        await model.OnGetAsync(customer.Id);

        Assert.Equal("09:00 UTC", model.DefaultSendTimesDisplay);
    }

    [Fact]
    public async Task OnPostUpdateSendTimesAsync_EmptyTimes_UsesSystemInfoDefaults()
    {
        var customer = await SeedCustomerAsync();
        customer.SendTimesLocal = "19:30";
        await _db.SaveChangesAsync();

        // SystemInfo has 08:45 (written by the worker), but _emailSettings has default empty list
        _db.SystemInfo.Add(new SystemInfo
        {
            Key = "email.send_times_utc",
            Value = "08:45",
            Category = "Email"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        model.EditSendTimesLocal = "";

        await model.OnPostUpdateSendTimesAsync(customer.Id);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal("", updated!.SendTimesLocal);
        Assert.NotNull(updated.NextSendTimeUtc);
        // Should use 08:45 from SystemInfo, not 08:00 hardcoded fallback
        Assert.Equal(45, updated.NextSendTimeUtc!.Value.Minute);
    }

    [Fact]
    public async Task OnPostUpdateTimeZoneAsync_EmptyTimes_UsesSystemInfoDefaults()
    {
        var customer = await SeedCustomerAsync();
        customer.SendTimesLocal = "";
        await _db.SaveChangesAsync();

        _db.SystemInfo.Add(new SystemInfo
        {
            Key = "email.send_times_utc",
            Value = "08:45",
            Category = "Email"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        model.EditTimeZoneId = "UTC";

        await model.OnPostUpdateTimeZoneAsync(customer.Id);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.NotNull(updated!.NextSendTimeUtc);
        Assert.Equal(45, updated.NextSendTimeUtc!.Value.Minute);
    }
}
