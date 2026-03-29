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
    public async Task OnPostUpdateSettingsAsync_ValidData_UpdatesCustomer()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Updated Name";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "UTC";
        model.EditIncludeLatestLocations = true;

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Customer settings updated.", model.SuccessMessage);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal("Updated Name", updated!.Name);
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_EmptyName_ReturnsError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "UTC";

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

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
    public async Task OnPostUpdateSettingsAsync_ValidData_UpdatesAllEmailFields()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "updated@example.com";
        model.EditCcEmails = "cc@example.com";
        model.EditBccEmails = "bcc@example.com";
        model.EditTimeZoneId = "UTC";
        model.EditIncludeLatestLocations = true;

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Customer settings updated.", model.SuccessMessage);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal("updated@example.com", updated!.Email);
        Assert.Equal("cc@example.com", updated.CcEmails);
        Assert.Equal("bcc@example.com", updated.BccEmails);
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_InvalidCustomer_ReturnsNotFound()
    {
        var model = CreatePageModel();
        model.EditName = "Name";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "UTC";

        var result = await model.OnPostUpdateSettingsAsync(999);

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
    public async Task OnPostUpdateSettingsAsync_ValidTimes_UpdatesCustomer()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "UTC";
        model.EditSendTimesLocal = "07:00, 19:30";
        model.EditIncludeLatestLocations = true;

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Customer settings updated.", model.SuccessMessage);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal("07:00, 19:30", updated!.SendTimesLocal);
        Assert.NotNull(updated.NextSendTimeUtc);
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_EmptyTimes_ClearsToDefault()
    {
        var customer = await SeedCustomerAsync();
        customer.SendTimesLocal = "19:30";
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "UTC";
        model.EditSendTimesLocal = "";
        model.EditIncludeLatestLocations = true;

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal("", updated!.SendTimesLocal);
        Assert.NotNull(updated.NextSendTimeUtc); // Falls back to default
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_InvalidTimes_ReturnsError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "UTC";
        model.EditSendTimesLocal = "invalid";

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditSendTimesLocal"));
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_ValidTimezone_UpdatesCustomer()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "America/New_York";
        model.EditIncludeLatestLocations = true;

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Customer settings updated.", model.SuccessMessage);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal("America/New_York", updated!.TimeZoneId);
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_InvalidTimezone_ReturnsError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "Invalid/Timezone";

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditTimeZoneId"));
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_RecomputesNextSendTime()
    {
        var customer = await SeedCustomerAsync();
        customer.SendTimesLocal = "19:30";
        customer.NextSendTimeUtc = DateTime.UtcNow.AddDays(1);
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "America/New_York";
        model.EditSendTimesLocal = "19:30";
        model.EditIncludeLatestLocations = true;

        await model.OnPostUpdateSettingsAsync(customer.Id);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.NotNull(updated!.NextSendTimeUtc);
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_WindowSettings_SavedToCustomer()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "UTC";
        model.EditSummaryWindowHours = 6;
        model.EditSummaryWindowCount = 4;
        model.EditSummaryWindowOffsetHours = 2;
        model.EditIncludeLatestLocations = false;

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal(6, updated!.SummaryWindowHours);
        Assert.Equal(4, updated.SummaryWindowCount);
        Assert.Equal(2, updated.SummaryWindowOffsetHours);
        Assert.False(updated.IncludeLatestLocations);
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_NullWindowSettings_ClearsOverrides()
    {
        var customer = await SeedCustomerAsync();
        customer.SummaryWindowHours = 6;
        customer.SummaryWindowCount = 4;
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "UTC";
        model.EditSummaryWindowHours = null;
        model.EditSummaryWindowCount = null;
        model.EditSummaryWindowOffsetHours = null;
        model.EditIncludeLatestLocations = true;

        await model.OnPostUpdateSettingsAsync(customer.Id);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Null(updated!.SummaryWindowHours);
        Assert.Null(updated.SummaryWindowCount);
        Assert.Null(updated.SummaryWindowOffsetHours);
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_InvalidWindowHours_ReturnsError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "UTC";
        model.EditSummaryWindowHours = 0;

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditSummaryWindowHours"));
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_InvalidWindowCount_ReturnsError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "UTC";
        model.EditSummaryWindowCount = 0;

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditSummaryWindowCount"));
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_NegativeWindowOffset_ReturnsError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "UTC";
        model.EditSummaryWindowOffsetHours = -1;

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditSummaryWindowOffsetHours"));
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_ValidWindowOffset_Saves()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "UTC";
        model.EditSummaryWindowOffsetHours = 0;
        model.EditIncludeLatestLocations = true;

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Customer settings updated.", model.SuccessMessage);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal(0, updated!.SummaryWindowOffsetHours);
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_IncludeLatestLocationsFalse_Saves()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "UTC";
        model.EditIncludeLatestLocations = false;

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.False(updated!.IncludeLatestLocations);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesEmailSettingFields()
    {
        var customer = await SeedCustomerAsync();
        customer.SummaryWindowHours = 6;
        customer.SummaryWindowCount = 3;
        customer.SummaryWindowOffsetHours = 2;
        customer.IncludeLatestLocations = false;
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.Equal(6, model.EditSummaryWindowHours);
        Assert.Equal(3, model.EditSummaryWindowCount);
        Assert.Equal(2, model.EditSummaryWindowOffsetHours);
        Assert.False(model.EditIncludeLatestLocations);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesDefaultWindowSettings()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.Equal(4, model.DefaultWindowHours);
        Assert.Equal(7, model.DefaultWindowCount);
        Assert.Equal(1, model.DefaultWindowOffset);
    }

    [Fact]
    public async Task OnGetAsync_ActivitySummary_UsesPerCustomerWindowSettings()
    {
        var customer = await SeedCustomerAsync();
        customer.SummaryWindowHours = 6;
        customer.SummaryWindowCount = 2;
        customer.SummaryWindowOffsetHours = 0;
        await _db.SaveChangesAsync();

        var hub = await SeedHubAsync(customer.Id);
        var device = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "dev-001",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Sensor"
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        // With windowCount=2, the activity summary should have exactly 2 windows
        Assert.Equal(2, model.ActivityWindows.Count);
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
    public async Task OnPostUpdateSettingsAsync_EmptyTimes_UsesSystemInfoDefaults()
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
        model.EditName = "Test User";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "UTC";
        model.EditSendTimesLocal = "";
        model.EditIncludeLatestLocations = true;

        await model.OnPostUpdateSettingsAsync(customer.Id);

        var updated = await _db.Customers.FindAsync(customer.Id);
        Assert.Equal("", updated!.SendTimesLocal);
        Assert.NotNull(updated.NextSendTimeUtc);
        // Should use 08:45 from SystemInfo, not 08:00 hardcoded fallback
        Assert.Equal(45, updated.NextSendTimeUtc!.Value.Minute);
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
    public async Task OnPostUpdateSettingsAsync_InvalidToEmail_ReturnsValidationError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "not-an-email";
        model.EditCcEmails = "";
        model.EditBccEmails = "";
        model.EditTimeZoneId = "UTC";

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditEmail"));
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_EmptyToEmail_ReturnsValidationError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "";
        model.EditCcEmails = "";
        model.EditBccEmails = "";
        model.EditTimeZoneId = "UTC";

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditEmail"));
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_InvalidCcEmail_ReturnsValidationError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "valid@example.com";
        model.EditCcEmails = "valid@test.com, not-valid";
        model.EditBccEmails = "";
        model.EditTimeZoneId = "UTC";

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditCcEmails"));
        Assert.Null(model.SuccessMessage);
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_InvalidBccEmail_ReturnsValidationError()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "valid@example.com";
        model.EditCcEmails = "";
        model.EditBccEmails = "bad-email";
        model.EditTimeZoneId = "UTC";

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("EditBccEmails"));
        Assert.Null(model.SuccessMessage);
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_ValidMultipleEmails_Succeeds()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "a@example.com, b@example.com";
        model.EditCcEmails = "cc1@test.com, cc2@test.com";
        model.EditBccEmails = "bcc@test.com";
        model.EditTimeZoneId = "UTC";
        model.EditIncludeLatestLocations = true;

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Customer settings updated.", model.SuccessMessage);
    }

    [Fact]
    public async Task OnPostUpdateSettingsAsync_EmptyCcBcc_Succeeds()
    {
        var customer = await SeedCustomerAsync();

        var model = CreatePageModel();
        model.EditName = "Test User";
        model.EditEmail = "valid@example.com";
        model.EditCcEmails = "";
        model.EditBccEmails = null;
        model.EditTimeZoneId = "UTC";
        model.EditIncludeLatestLocations = true;

        var result = await model.OnPostUpdateSettingsAsync(customer.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Customer settings updated.", model.SuccessMessage);
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
    public async Task OnPostUpdateSettingsAsync_InvalidCustomer_Emails_ReturnsNotFound()
    {
        var model = CreatePageModel();
        model.EditName = "Test";
        model.EditEmail = "test@example.com";
        model.EditTimeZoneId = "UTC";

        var result = await model.OnPostUpdateSettingsAsync(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostToggleStatusAsync_InvalidCustomer_ReturnsNotFound()
    {
        var model = CreatePageModel();

        var result = await model.OnPostToggleStatusAsync(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_LoadsBatteryStatuses_WithValidReadings()
    {
        var customer = await SeedCustomerAsync();
        var hub = await SeedHubAsync(customer.Id);

        var batteryDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "bat-001",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Hallway Sensor"
        };
        _db.Devices.Add(batteryDevice);
        await _db.SaveChangesAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = batteryDevice.Id,
            Timestamp = DateTime.UtcNow.AddHours(-1),
            ReadingType = ReadingTypes.Battery,
            Value = "{\"battery_level\":42,\"battery_state\":\"normal\"}"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.Single(model.BatteryStatuses);
        Assert.Equal("Hallway Sensor", model.BatteryStatuses[0].DeviceName);
        Assert.Equal(42, model.BatteryStatuses[0].BatteryLevel);
        Assert.Equal("normal", model.BatteryStatuses[0].BatteryState);
    }

    [Fact]
    public async Task OnGetAsync_BatteryStatuses_SortedByLevelAscending()
    {
        var customer = await SeedCustomerAsync();
        var hub = await SeedHubAsync(customer.Id);

        var bat1 = new Device { HubId = hub.Id, HueDeviceId = "bat-high", DeviceType = DeviceTypes.MotionSensor, Name = "High" };
        var bat2 = new Device { HubId = hub.Id, HueDeviceId = "bat-low", DeviceType = DeviceTypes.MotionSensor, Name = "Low" };
        _db.Devices.AddRange(bat1, bat2);
        await _db.SaveChangesAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = bat1.Id, Timestamp = DateTime.UtcNow.AddHours(-1),
            ReadingType = ReadingTypes.Battery, Value = "{\"battery_level\":90,\"battery_state\":\"normal\"}"
        });
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = bat2.Id, Timestamp = DateTime.UtcNow.AddHours(-1),
            ReadingType = ReadingTypes.Battery, Value = "{\"battery_level\":15,\"battery_state\":\"low\"}"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.Equal(2, model.BatteryStatuses.Count);
        Assert.Equal("Low", model.BatteryStatuses[0].DeviceName);
        Assert.Equal("High", model.BatteryStatuses[1].DeviceName);
    }

    [Fact]
    public async Task OnGetAsync_BatteryStatuses_ShowsLatestReadingPerDevice()
    {
        var customer = await SeedCustomerAsync();
        var hub = await SeedHubAsync(customer.Id);

        var batteryDevice = new Device
        {
            HubId = hub.Id, HueDeviceId = "bat-multi", DeviceType = DeviceTypes.MotionSensor, Name = "Study"
        };
        _db.Devices.Add(batteryDevice);
        await _db.SaveChangesAsync();

        // Older reading at 80%, newer reading at 20%
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = batteryDevice.Id, Timestamp = DateTime.UtcNow.AddDays(-2),
            ReadingType = ReadingTypes.Battery, Value = "{\"battery_level\":80,\"battery_state\":\"normal\"}"
        });
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = batteryDevice.Id, Timestamp = DateTime.UtcNow.AddHours(-1),
            ReadingType = ReadingTypes.Battery, Value = "{\"battery_level\":20,\"battery_state\":\"low\"}"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.Single(model.BatteryStatuses);
        Assert.Equal(20, model.BatteryStatuses[0].BatteryLevel);
        Assert.Equal("low", model.BatteryStatuses[0].BatteryState);
    }

    [Fact]
    public async Task OnGetAsync_NoBatteryDevices_EmptyBatteryStatuses()
    {
        var customer = await SeedCustomerAsync();
        await SeedHubAsync(customer.Id);

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.Empty(model.BatteryStatuses);
    }

    [Fact]
    public async Task OnGetAsync_MalformedBatteryJson_SkipsGracefully()
    {
        var customer = await SeedCustomerAsync();
        var hub = await SeedHubAsync(customer.Id);

        var batteryDevice = new Device
        {
            HubId = hub.Id, HueDeviceId = "bat-bad", DeviceType = DeviceTypes.MotionSensor, Name = "Bad Sensor"
        };
        _db.Devices.Add(batteryDevice);
        await _db.SaveChangesAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = batteryDevice.Id, Timestamp = DateTime.UtcNow.AddHours(-1),
            ReadingType = ReadingTypes.Battery, Value = "not-valid-json"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.Empty(model.BatteryStatuses);
    }

    [Fact]
    public async Task OnGetAsync_LoadsEmailPreviewHtml()
    {
        var customer = await SeedCustomerAsync();
        var hub = await SeedHubAsync(customer.Id);

        var device = new Device
        {
            HubId = hub.Id, HueDeviceId = "dev-001", DeviceType = DeviceTypes.MotionSensor, Name = "Sensor"
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = DateTime.UtcNow.AddHours(-2),
            ReadingType = ReadingTypes.Motion,
            Value = $"{{\"motion\":true,\"changed\":\"{DateTime.UtcNow.AddHours(-2):O}\"}}"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.NotEmpty(model.EmailPreviewHtml);
        Assert.Contains("Daily Activity Summary", model.EmailPreviewHtml);
    }

    [Fact]
    public async Task OnGetAsync_EmailPreview_ContainsCustomerName()
    {
        var customer = await SeedCustomerAsync(name: "Alice Smith");
        await SeedHubAsync(customer.Id);

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.Contains("for Alice Smith", model.EmailPreviewHtml);
    }

    [Fact]
    public async Task OnGetAsync_LoadsUnreachableDevices()
    {
        var customer = await SeedCustomerAsync();
        var hub = await SeedHubAsync(customer.Id);

        var device = new Device
        {
            HubId = hub.Id, HueDeviceId = "conn-001", DeviceType = DeviceTypes.MotionSensor, Name = "Front Door"
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id, Timestamp = DateTime.UtcNow.AddMinutes(-30),
            ReadingType = ReadingTypes.ZigbeeConnectivity, Value = "{\"status\":\"connectivity_issue\"}"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.Single(model.UnreachableDevices);
        Assert.Equal("Front Door", model.UnreachableDevices[0].DeviceName);
        Assert.Equal("connectivity_issue", model.UnreachableDevices[0].Status);
    }

    [Fact]
    public async Task OnGetAsync_ConnectedDevices_NotInUnreachableList()
    {
        var customer = await SeedCustomerAsync();
        var hub = await SeedHubAsync(customer.Id);

        var device = new Device
        {
            HubId = hub.Id, HueDeviceId = "conn-002", DeviceType = DeviceTypes.MotionSensor, Name = "Kitchen"
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id, Timestamp = DateTime.UtcNow.AddMinutes(-30),
            ReadingType = ReadingTypes.ZigbeeConnectivity, Value = "{\"status\":\"connected\"}"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.Empty(model.UnreachableDevices);
    }

    [Fact]
    public async Task OnGetAsync_MalformedConnectivityJson_SkipsGracefully()
    {
        var customer = await SeedCustomerAsync();
        var hub = await SeedHubAsync(customer.Id);

        var device = new Device
        {
            HubId = hub.Id, HueDeviceId = "conn-bad", DeviceType = DeviceTypes.MotionSensor, Name = "Bad Sensor"
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id, Timestamp = DateTime.UtcNow.AddMinutes(-30),
            ReadingType = ReadingTypes.ZigbeeConnectivity, Value = "not-valid-json"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.Empty(model.UnreachableDevices);
    }

    [Fact]
    public async Task OnGetAsync_UnreachableDevices_SortedByName()
    {
        var customer = await SeedCustomerAsync();
        var hub = await SeedHubAsync(customer.Id);

        var dev1 = new Device { HubId = hub.Id, HueDeviceId = "conn-z", DeviceType = DeviceTypes.MotionSensor, Name = "Zebra" };
        var dev2 = new Device { HubId = hub.Id, HueDeviceId = "conn-a", DeviceType = DeviceTypes.MotionSensor, Name = "Alpha" };
        _db.Devices.AddRange(dev1, dev2);
        await _db.SaveChangesAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = dev1.Id, Timestamp = DateTime.UtcNow.AddMinutes(-30),
            ReadingType = ReadingTypes.ZigbeeConnectivity, Value = "{\"status\":\"connectivity_issue\"}"
        });
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = dev2.Id, Timestamp = DateTime.UtcNow.AddMinutes(-30),
            ReadingType = ReadingTypes.ZigbeeConnectivity, Value = "{\"status\":\"unidirectional_incoming\"}"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync(customer.Id);

        Assert.Equal(2, model.UnreachableDevices.Count);
        Assert.Equal("Alpha", model.UnreachableDevices[0].DeviceName);
        Assert.Equal("Zebra", model.UnreachableDevices[1].DeviceName);
    }

    [Fact]
    public async Task OnGetAsync_NullWindowOffset_FallsBackToDefault()
    {
        var customer = await SeedCustomerAsync();
        customer.SummaryWindowOffsetHours = null;
        customer.SummaryWindowHours = null;
        customer.SummaryWindowCount = null;
        await _db.SaveChangesAsync();

        var emailSettings = new EmailSettings
        {
            SummaryWindowHours = 4,
            SummaryWindowCount = 3,
            SummaryWindowOffsetHours = 2
        };

        var model = CreatePageModel(emailSettingsOverride: emailSettings);
        await model.OnGetAsync(customer.Id);

        // Should use defaults from EmailSettings: 3 windows
        Assert.Equal(3, model.ActivityWindows.Count);
        Assert.Equal(2, model.DefaultWindowOffset);
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
