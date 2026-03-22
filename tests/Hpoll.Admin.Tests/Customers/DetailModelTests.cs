using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Hpoll.Admin.Pages.Customers;
using Hpoll.Admin.Services;
using Hpoll.Core.Configuration;
using Hpoll.Core.Constants;
using Hpoll.Core.Interfaces;
using Hpoll.Data;
using Hpoll.Data.Entities;
using Hpoll.Email;

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

    private DetailModel CreatePageModel(HueAppSettings? hueAppSettings = null, EmailSettings? emailSettingsOverride = null)
    {
        var hueApp = Options.Create(hueAppSettings ?? new HueAppSettings());
        var emailSettings = Options.Create(emailSettingsOverride ?? new EmailSettings());
        var sendTimeService = new SendTimeDisplayService(_db, emailSettings);
        var emailRenderer = new EmailRenderer(_db, NullLogger<EmailRenderer>.Instance, emailSettings);
        var model = new DetailModel(_db, hueApp, emailSettings, sendTimeService, emailRenderer, NullLogger<DetailModel>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Session = new TestSession();
        model.PageContext = new PageContext
        {
            ActionDescriptor = new CompiledPageActionDescriptor(),
            HttpContext = httpContext,
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
    public async Task OnGetAsync_AlwaysLoadsActivitySummary()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        var result = await model.OnGetAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.ActivityWindows);
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

        var model = CreatePageModel(emailSettingsOverride: new EmailSettings { SendTimesUtc = new List<string> { "09:00" } });

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

    [Fact]
    public async Task OnGetAsync_DefaultEditTz_SetsEditingTimeZoneFalse()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.False(model.EditingTimeZone);
    }

    [Fact]
    public async Task OnGetAsync_EditTzTrue_SetsEditingTimeZoneTrue()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id, editTz: true);

        Assert.True(model.EditingTimeZone);
    }

    [Fact]
    public async Task OnGetAsync_EditTzFalse_SetsEditingTimeZoneFalse()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id, editTz: false);

        Assert.False(model.EditingTimeZone);
    }

    [Fact]
    public async Task OnPostUpdateTimeZoneAsync_EmptyTimezone_SetsEditingTimeZoneTrue()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditTimeZoneId = "";

        await model.OnPostUpdateTimeZoneAsync(customer.Id);

        Assert.True(model.EditingTimeZone);
        Assert.True(model.ModelState.ContainsKey("EditTimeZoneId"));
    }

    [Fact]
    public async Task OnPostUpdateTimeZoneAsync_InvalidTimezone_SetsEditingTimeZoneTrue()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditTimeZoneId = "Invalid/Timezone";

        await model.OnPostUpdateTimeZoneAsync(customer.Id);

        Assert.True(model.EditingTimeZone);
    }

    [Fact]
    public async Task OnPostUpdateTimeZoneAsync_ValidTimezone_DoesNotSetEditingTimeZone()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditTimeZoneId = "UTC";

        await model.OnPostUpdateTimeZoneAsync(customer.Id);

        Assert.False(model.EditingTimeZone);
    }

    [Fact]
    public async Task OnPostRegisterHubAsync_InvalidCustomer_ReturnsNotFound()
    {
        var model = CreatePageModel(new HueAppSettings { ClientId = "id", CallbackUrl = "http://callback" });
        var result = await model.OnPostRegisterHubAsync(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostRegisterHubAsync_MissingClientId_ReturnsPageWithError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel(new HueAppSettings { ClientId = "", CallbackUrl = "http://callback" });
        var result = await model.OnPostRegisterHubAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Contains("ClientId", model.ErrorMessage);
        Assert.Equal(customer.Id, model.Customer.Id);
    }

    [Fact]
    public async Task OnPostRegisterHubAsync_ValidConfig_SetsOAuthUrlAndSession()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel(new HueAppSettings
        {
            ClientId = "test-client-id",
            CallbackUrl = "https://example.com/callback"
        });

        var result = await model.OnPostRegisterHubAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.OAuthUrl);
        Assert.Contains("test-client-id", model.OAuthUrl);
        Assert.Contains("example.com%2Fcallback", model.OAuthUrl);
        Assert.Contains("response_type=code", model.OAuthUrl);

        // Verify session state was set
        var session = model.PageContext.HttpContext.Session;
        Assert.Equal(customer.Id, session.GetInt32("OAuthCustomerId"));
        Assert.NotNull(session.GetString("OAuthCsrf"));
    }

    [Fact]
    public async Task OnPostUpdateEmailsAsync_InvalidToEmail_ReturnsValidationError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditEmail = "not-an-email";
        model.EditCcEmails = "";
        model.EditBccEmails = "";

        var result = await model.OnPostUpdateEmailsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditEmail"));
    }

    [Fact]
    public async Task OnPostUpdateEmailsAsync_EmptyToEmail_ReturnsValidationError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditEmail = "";
        model.EditCcEmails = "";
        model.EditBccEmails = "";

        var result = await model.OnPostUpdateEmailsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditEmail"));
    }

    [Fact]
    public async Task OnPostUpdateEmailsAsync_InvalidCcEmail_ReturnsValidationError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditEmail = "valid@example.com";
        model.EditCcEmails = "valid@test.com, not-valid";
        model.EditBccEmails = "";

        var result = await model.OnPostUpdateEmailsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditCcEmails"));
        Assert.Null(model.SuccessMessage);
    }

    [Fact]
    public async Task OnPostUpdateEmailsAsync_InvalidBccEmail_ReturnsValidationError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditEmail = "valid@example.com";
        model.EditCcEmails = "";
        model.EditBccEmails = "bad-email";

        var result = await model.OnPostUpdateEmailsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditBccEmails"));
        Assert.Null(model.SuccessMessage);
    }

    [Fact]
    public async Task OnPostUpdateEmailsAsync_ValidMultipleEmails_Succeeds()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditEmail = "a@example.com, b@example.com";
        model.EditCcEmails = "cc1@test.com, cc2@test.com";
        model.EditBccEmails = "bcc@test.com";

        var result = await model.OnPostUpdateEmailsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Email addresses updated.", model.SuccessMessage);
    }

    [Fact]
    public async Task OnPostUpdateEmailsAsync_EmptyCcBcc_Succeeds()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditEmail = "valid@example.com";
        model.EditCcEmails = "";
        model.EditBccEmails = null;

        var result = await model.OnPostUpdateEmailsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Email addresses updated.", model.SuccessMessage);
    }

    [Fact]
    public async Task OnPostRegisterHubAsync_PopulatesEditFieldsFromCustomer()
    {
        var customer = await SeedCustomerAsync("Custom Name", "custom@example.com");
        customer.CcEmails = "cc@test.com";
        customer.BccEmails = "bcc@test.com";
        await _db.SaveChangesAsync();

        var model = CreatePageModel(new HueAppSettings
        {
            ClientId = "test-client-id",
            CallbackUrl = "https://example.com/callback"
        });

        await model.OnPostRegisterHubAsync(customer.Id);

        Assert.Equal("custom@example.com", model.EditEmail);
        Assert.Equal("Custom Name", model.EditName);
        Assert.Equal("cc@test.com", model.EditCcEmails);
        Assert.Equal("bcc@test.com", model.EditBccEmails);
    }

    [Fact]
    public async Task OnPostUpdateEmailsAsync_InvalidCustomer_ReturnsNotFound()
    {
        var model = CreatePageModel();
        model.EditEmail = "test@example.com";

        var result = await model.OnPostUpdateEmailsAsync(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostToggleStatusAsync_InvalidCustomer_ReturnsNotFound()
    {
        var model = CreatePageModel();

        var result = await model.OnPostToggleStatusAsync(999);

        Assert.IsType<NotFoundResult>(result);
    }

    private class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public string Id => "test-session";
        public bool IsAvailable => true;
        public IEnumerable<string> Keys => _store.Keys;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value)
        {
            if (_store.TryGetValue(key, out var stored))
            {
                value = stored;
                return true;
            }
            value = Array.Empty<byte>();
            return false;
        }
    }
}
