using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Hpoll.Admin.Pages.Customers;
using Hpoll.Admin.Services;
using Hpoll.Core.Configuration;
using Hpoll.Core.Constants;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Tests.Customers;

public class CreateModelTests : IDisposable
{
    private readonly HpollDbContext _db;

    public CreateModelTests()
    {
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new HpollDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private CreateModel CreatePageModel(EmailSettings? emailSettings = null)
    {
        var opts = Options.Create(emailSettings ?? new EmailSettings());
        var sendTimeService = new SendTimeDisplayService(_db, opts);
        var model = new CreateModel(_db, sendTimeService);
        model.PageContext = new PageContext
        {
            ActionDescriptor = new CompiledPageActionDescriptor(),
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData()
        };
        return model;
    }

    [Fact]
    public async Task OnPostAsync_ValidInput_CreatesCustomer()
    {
        var model = CreatePageModel();
        model.Name = "Alice Smith";
        model.Email = "alice@example.com";
        model.TimeZoneId = "UTC";

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Detail", redirect.PageName);

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == "alice@example.com");
        Assert.NotNull(customer);
        Assert.Equal("Alice Smith", customer.Name);
        Assert.Equal(CustomerStatus.Active, customer.Status);
    }

    [Fact]
    public async Task OnPostAsync_ValidInput_SetsSendTimesAndNextSendTime()
    {
        var model = CreatePageModel();
        model.Name = "Alice Smith";
        model.Email = "alice@example.com";
        model.TimeZoneId = "UTC";
        model.SendTimesLocal = "19:30";

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == "alice@example.com");
        Assert.NotNull(customer);
        Assert.Equal("19:30", customer.SendTimesLocal);
        Assert.NotNull(customer.NextSendTimeUtc);
    }

    [Fact]
    public void DefaultSendTimesLocal_IsNull()
    {
        var model = CreatePageModel();
        Assert.Null(model.SendTimesLocal);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesDefaultSendTimesDisplay_FromEmailSettings()
    {
        var model = CreatePageModel(new EmailSettings
        {
            SendTimesUtc = new List<string> { "08:00", "20:00" }
        });

        await model.OnGetAsync();

        Assert.Equal("08:00, 20:00 UTC", model.DefaultSendTimesDisplay);
    }

    [Fact]
    public async Task OnGetAsync_EmptyEmailSettings_ShowsEmptyDefault()
    {
        var model = CreatePageModel();

        await model.OnGetAsync();

        Assert.Equal(" UTC", model.DefaultSendTimesDisplay);
    }

    [Fact]
    public async Task OnGetAsync_SystemInfoOverridesEmailSettings()
    {
        _db.SystemInfo.Add(new SystemInfo
        {
            Key = "email.send_times_utc",
            Value = "08:45",
            Category = "Email"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();

        await model.OnGetAsync();

        Assert.Equal("08:45 UTC", model.DefaultSendTimesDisplay);
    }

    [Fact]
    public async Task OnPostAsync_InvalidEmailFormat_ReturnsValidationError()
    {
        var model = CreatePageModel();
        model.Name = "Bob";
        model.Email = "not-an-email";
        model.TimeZoneId = "UTC";

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("Email"));
    }

    [Fact]
    public async Task OnPostAsync_MixedValidInvalidEmails_ReturnsValidationError()
    {
        var model = CreatePageModel();
        model.Name = "Bob";
        model.Email = "valid@example.com, bad-email";
        model.TimeZoneId = "UTC";

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("Email"));
    }

    [Fact]
    public async Task OnPostAsync_MultipleValidEmails_Succeeds()
    {
        var model = CreatePageModel();
        model.Name = "Bob";
        model.Email = "a@example.com, b@example.com";
        model.TimeZoneId = "UTC";

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_InvalidTimezone_ReturnsError()
    {
        var model = CreatePageModel();
        model.Name = "Bob";
        model.Email = "bob@example.com";
        model.TimeZoneId = "Invalid/Timezone";

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("TimeZoneId"));
    }

    [Fact]
    public async Task OnPostAsync_InvalidSendTimes_ReturnsError()
    {
        var model = CreatePageModel();
        model.Name = "Bob";
        model.Email = "bob@example.com";
        model.TimeZoneId = "UTC";
        model.SendTimesLocal = "invalid";

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("SendTimesLocal"));
    }

    [Fact]
    public async Task OnPostAsync_InvalidModelState_ReturnsPage()
    {
        var model = CreatePageModel();
        model.ModelState.AddModelError("Name", "Name is required.");

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_EmptySendTimes_UsesDefaultAndSetsNextSendTime()
    {
        var model = CreatePageModel();
        model.Name = "Alice Smith";
        model.Email = "alice@example.com";
        model.TimeZoneId = "UTC";
        model.SendTimesLocal = "";

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == "alice@example.com");
        Assert.NotNull(customer);
        Assert.Equal("", customer.SendTimesLocal);
        Assert.NotNull(customer.NextSendTimeUtc); // Should fall back to default
    }

    [Fact]
    public async Task OnPostAsync_EmptySendTimes_UsesSystemInfoDefaults()
    {
        // SystemInfo has 08:45 (written by the worker), but _emailSettings has default empty list
        _db.SystemInfo.Add(new SystemInfo
        {
            Key = "email.send_times_utc",
            Value = "08:45",
            Category = "Email"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        model.Name = "Alice Smith";
        model.Email = "alice@example.com";
        model.TimeZoneId = "UTC";
        model.SendTimesLocal = "";

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == "alice@example.com");
        Assert.NotNull(customer);
        Assert.Equal("", customer.SendTimesLocal);
        Assert.NotNull(customer.NextSendTimeUtc);
        // Should use 08:45 from SystemInfo, not 08:00 hardcoded fallback
        Assert.Equal(45, customer.NextSendTimeUtc!.Value.Minute);
    }

    [Fact]
    public async Task OnPostAsync_ValidationError_PreservesDefaultSendTimesDisplay()
    {
        _db.SystemInfo.Add(new SystemInfo
        {
            Key = "email.send_times_utc",
            Value = "08:45",
            Category = "Email"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        model.Name = "Bob";
        model.Email = "not-an-email";
        model.TimeZoneId = "UTC";

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("08:45 UTC", model.DefaultSendTimesDisplay);
    }
}
