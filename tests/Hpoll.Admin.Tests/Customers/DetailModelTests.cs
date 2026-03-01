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
        var customer = new Customer { Name = name, Email = email, TimeZoneId = "UTC", Status = "active" };
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
            Status = "active"
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
        Assert.Equal("inactive", updated!.Status);
    }

    [Fact]
    public async Task OnPostToggleStatusAsync_InactiveToActive_TogglesBack()
    {
        var customer = await SeedCustomerAsync();
        customer.Status = "inactive";
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        var result = await model.OnPostToggleStatusAsync(customer.Id);

        Assert.IsType<RedirectToPageResult>(result);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal("active", updated!.Status);
    }

    [Fact]
    public async Task OnPostUpdateCcBccAsync_ValidData_UpdatesCcBcc()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditCcEmails = "cc@example.com";
        model.EditBccEmails = "bcc@example.com";

        var result = await model.OnPostUpdateCcBccAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("CC/BCC lists updated.", model.SuccessMessage);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal("cc@example.com", updated!.CcEmails);
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
}
