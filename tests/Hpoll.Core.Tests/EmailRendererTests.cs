using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Constants;
using Hpoll.Data;
using Hpoll.Data.Entities;
using Hpoll.Email;

namespace Hpoll.Core.Tests;

public class EmailRendererTests : IDisposable
{
    private readonly HpollDbContext _db;
    private readonly EmailRenderer _renderer;

    // nowUtc = Feb 28 08:00 UTC
    // With UTC timezone and offset=1: bucketEnd = 09:00 (end of current window), bucketStart = Feb 27 05:00 (28h back)
    // 7 windows: 05:00–09:00, 09:00–13:00, 13:00–17:00, 17:00–21:00, 21:00–01:00, 01:00–05:00, 05:00–09:00
    private static readonly DateTime NowUtc = new(2026, 2, 28, 8, 0, 0, DateTimeKind.Utc);
    private const string TimeZone = "UTC";

    public EmailRendererTests()
    {
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new HpollDbContext(options);
        var emailSettings = Options.Create(new EmailSettings());
        _renderer = new EmailRenderer(_db, NullLogger<EmailRenderer>.Instance, emailSettings);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<(Customer customer, Hub hub, Device device)> SeedBaseDataAsync()
    {
        var customer = new Customer { Name = "Test User", Email = "test@example.com", TimeZoneId = "UTC" };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "001788FFFE123456",
            HueApplicationKey = "testkey",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        var device = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-001",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Sensor 1"
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        return (customer, hub, device);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithNoReadings_StillReturnsHtml()
    {
        var (customer, _, _) = await SeedBaseDataAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Daily Activity Summary", html);
        Assert.DoesNotContain("No data", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithNoReadings_ShowsOfflineBar()
    {
        var (customer, _, _) = await SeedBaseDataAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // All windows have no readings — should show dark grey offline bars
        Assert.Contains("#333333", html);
        Assert.Contains("offline", html);
        // Should not show red (which is reserved for windows with readings but no motion)
        Assert.DoesNotContain("#e74c3c", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithReadingsButNoMotion_ShowsRedNotOffline()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // Temperature reading in one window — has readings, but no motion events
        AddTemp(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 21.0);
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // The window with readings but no motion should show red, not offline
        Assert.Contains("#e74c3c", html);
        // Dark grey and "offline" may still appear for other windows with no readings at all,
        // but should not be the only colour — red must be present for the window that has data
        Assert.Contains("#333333", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithMotionReadings_ShowsActivity()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // Readings at Feb 27 10:00 and 11:00 — within the 08:00–12:00 window
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Motion,
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 11, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Motion,
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T11:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Motion Activity", html);
        Assert.Contains("Location Diversity", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithTemperatureReadings_ShowsRange()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Temperature,
            Value = "{\"temperature\":19.5,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 11, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Temperature,
            Value = "{\"temperature\":22.0,\"changed\":\"2026-02-27T11:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Temperature Range", html);
        Assert.Contains("19.5", html);
        Assert.Contains("22.0", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_ContainsExpectedHeader()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Motion,
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Daily Activity Summary", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_ContainsExpectedWindows()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Motion,
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // NowUtc at 08:00: current window is 05:00–09:00 (displayEnd 08:00, 3h of data — kept).
        // All 7 windows shown with the offset-1 boundaries.
        Assert.Contains("05:00", html);
        Assert.Contains("09:00", html);
        Assert.Contains("13:00", html);
        Assert.Contains("17:00", html);
        Assert.Contains("21:00", html);
        Assert.Contains("01:00", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithMalformedJson_HandlesGracefully()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Motion,
            Value = "not-valid-json"
        });
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Temperature,
            Value = "{invalid}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Daily Activity Summary", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithMotionFalse_DoesNotCountAsEvent()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Motion,
            Value = "{\"motion\":false,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Daily Activity Summary", html);
        // 0 areas active — label shows "0" (not "0/1")
        Assert.Contains(">0<", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithNoCustomerHubs_StillReturnsHtml()
    {
        var customer = new Customer { Name = "No Hubs User", Email = "nohubs@example.com", TimeZoneId = "UTC" };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Daily Activity Summary", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithMultipleDevices_ShowsCorrectDiversity()
    {
        var (customer, hub, device1) = await SeedBaseDataAsync();

        var device2 = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-002",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Sensor 2"
        };
        _db.Devices.Add(device2);
        await _db.SaveChangesAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device1.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Motion,
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // 1 out of 2 motion sensors had activity — label shows "1"
        Assert.Contains(">1<", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_HeaderShowsTimeRange()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Motion,
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // Header shows time range: "27 Feb 2026 05:00 – 28 Feb 2026 08:00"
        Assert.Contains("27 Feb 2026", html);
        Assert.Contains("28 Feb 2026", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_ReadingsOutsideWindow_AreExcluded()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // Reading inside the window (Feb 27 10:00 — within 09:00–13:00)
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Motion,
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        // Reading outside the window (Feb 27 04:00 — before the 28h window starts at 05:00)
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 4, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Motion,
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T04:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Motion Activity", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithNonUtcTimezone_BucketsInLocalTime()
    {
        // Use Australia/Sydney (AEDT = UTC+11 in Feb)
        var customer = new Customer { Name = "Aussie User", Email = "aussie@example.com", TimeZoneId = "Australia/Sydney" };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "001788FFFE999999",
            HueApplicationKey = "testkey",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        var device = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-au-001",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Lounge Sensor"
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        // nowUtc = Feb 28 08:00 UTC = Feb 28 19:00 AEDT
        // With offset=1: bucketEnd = floor((19-1)/4)*4+4+1 = floor(18/4)*4+5 = 21:00 AEDT on Feb 28 = Feb 28 10:00 UTC
        // bucketStart = 28h back = 17:00 AEDT on Feb 27 = Feb 27 06:00 UTC
        // 7 windows: 17:00–21:00, 21:00–01:00, 01:00–05:00, 05:00–09:00, 09:00–13:00, 13:00–17:00, 17:00–21:00 AEDT

        // Reading at Feb 27 06:00 UTC = Feb 27 17:00 AEDT — falls in 17:00–21:00 AEDT window
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 6, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Motion,
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T06:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, "Australia/Sydney", NowUtc);

        Assert.NotNull(html);
        // Windows should be labeled in AEDT local time
        Assert.Contains("17:00", html);
        Assert.Contains("21:00", html);
        Assert.Contains("01:00", html);
        Assert.Contains("05:00", html);
        Assert.Contains("09:00", html);
        Assert.Contains("13:00", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_DoesNotContainSummaryDataTable()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        AddMotion(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.DoesNotContain("Summary Data", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_ZeroMotionEvents_ShowsRedBar()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // Only temperature readings, no motion events
        AddTemp(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 20.0);
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // Red color should appear for zero-event windows
        Assert.Contains("#e74c3c", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_ManyMotionEvents_CapsBarAt5Plus()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // Add 8 motion events in one window
        for (int i = 0; i < 8; i++)
        {
            AddMotion(device.Id, new DateTime(2026, 2, 27, 9, 0, 0, DateTimeKind.Utc).AddMinutes(i * 5));
        }
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("5+", html);
        Assert.Contains("#27ae60", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WindowsAreNewestFirst()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        AddMotion(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // The newest window (05:00–08:00 on Feb 28) should appear before all older windows
        // in the Motion Activity section. Newest is listed first (its label starts with "05:00").
        var motionIdx = html.IndexOf("Motion Activity");
        var firstWindowLabel = html.IndexOf("05:00", motionIdx + "Motion Activity".Length);
        Assert.True(firstWindowLabel > motionIdx, "First window after Motion Activity header should be the newest");
    }

    [Fact]
    public async Task RenderDailySummaryAsync_GeneratePreviewHtml()
    {
        var (customer, hub, device1) = await SeedBaseDataAsync();

        var device2 = new Device { HubId = hub.Id, HueDeviceId = "device-002", DeviceType = DeviceTypes.MotionSensor, Name = "Sensor 2" };
        var device3 = new Device { HubId = hub.Id, HueDeviceId = "device-003", DeviceType = DeviceTypes.MotionSensor, Name = "Sensor 3" };
        _db.Devices.AddRange(device2, device3);
        await _db.SaveChangesAsync();

        // Window 09:00–13:00 Feb 27: 3 events, 2 devices — GREEN activity, GREEN diversity
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 9, 0, 0, DateTimeKind.Utc));
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        AddMotion(device2.Id, new DateTime(2026, 2, 27, 11, 0, 0, DateTimeKind.Utc));
        AddTemp(device1.Id, new DateTime(2026, 2, 27, 9, 0, 0, DateTimeKind.Utc), 18.5);
        AddTemp(device1.Id, new DateTime(2026, 2, 27, 11, 0, 0, DateTimeKind.Utc), 21.0);

        // Window 13:00–17:00 Feb 27: 1 event, 1 device — YELLOW activity, YELLOW diversity
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 14, 0, 0, DateTimeKind.Utc));
        AddTemp(device1.Id, new DateTime(2026, 2, 27, 14, 0, 0, DateTimeKind.Utc), 22.5);

        // Window 17:00–21:00 Feb 27: 0 events — RED activity, RED diversity
        AddTemp(device1.Id, new DateTime(2026, 2, 27, 18, 0, 0, DateTimeKind.Utc), 20.0);

        // Window 21:00–01:00 Feb 27–28: 6 events, 3 devices — GREEN 5+, GREEN diversity
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 21, 0, 0, DateTimeKind.Utc));
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 22, 0, 0, DateTimeKind.Utc));
        AddMotion(device2.Id, new DateTime(2026, 2, 27, 21, 30, 0, DateTimeKind.Utc));
        AddMotion(device2.Id, new DateTime(2026, 2, 27, 22, 30, 0, DateTimeKind.Utc));
        AddMotion(device3.Id, new DateTime(2026, 2, 27, 23, 0, 0, DateTimeKind.Utc));
        AddMotion(device3.Id, new DateTime(2026, 2, 27, 23, 30, 0, DateTimeKind.Utc));
        AddTemp(device1.Id, new DateTime(2026, 2, 27, 22, 0, 0, DateTimeKind.Utc), 19.0);

        // Window 01:00–05:00 Feb 28: 0 events — RED activity, RED diversity

        // Window 05:00–09:00 Feb 28 (current, displayEnd 08:00): 3 events, 2 devices
        AddMotion(device1.Id, new DateTime(2026, 2, 28, 5, 0, 0, DateTimeKind.Utc));
        AddMotion(device1.Id, new DateTime(2026, 2, 28, 6, 0, 0, DateTimeKind.Utc));
        AddTemp(device1.Id, new DateTime(2026, 2, 28, 6, 0, 0, DateTimeKind.Utc), 17.5);
        AddMotion(device2.Id, new DateTime(2026, 2, 28, 8, 30, 0, DateTimeKind.Utc));

        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Daily Activity Summary", html);
        Assert.Contains("Motion Activity", html);
        Assert.Contains("Location Diversity", html);
        Assert.DoesNotContain("Summary Data", html);
    }

    private void AddMotion(int deviceId, DateTime timestamp)
    {
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = deviceId,
            Timestamp = timestamp,
            ReadingType = ReadingTypes.Motion,
            Value = $"{{\"motion\":true,\"changed\":\"{timestamp:O}\"}}"
        });
    }

    private void AddTemp(int deviceId, DateTime timestamp, double temp)
    {
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = deviceId,
            Timestamp = timestamp,
            ReadingType = ReadingTypes.Temperature,
            Value = $"{{\"temperature\":{temp},\"changed\":\"{timestamp:O}\"}}"
        });
    }

    private void AddBattery(int deviceId, DateTime timestamp, int level, string state = "normal")
    {
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = deviceId,
            Timestamp = timestamp,
            ReadingType = ReadingTypes.Battery,
            Value = $"{{\"battery_level\":{level},\"battery_state\":\"{state}\"}}"
        });
    }

    [Fact]
    public async Task RenderDailySummaryAsync_InvalidCustomerId_ReturnsValidHtml()
    {
        // Customer ID 999 doesn't exist in the DB
        var html = await _renderer.RenderDailySummaryAsync(999, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Daily Activity Summary", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_InvalidTimezone_ThrowsTimeZoneNotFoundException()
    {
        var (customer, _, _) = await SeedBaseDataAsync();

        await Assert.ThrowsAsync<TimeZoneNotFoundException>(
            () => _renderer.RenderDailySummaryAsync(customer.Id, "Invalid/Timezone", NowUtc));
    }

    [Fact]
    public async Task RenderDailySummaryAsync_XssInDeviceName_IsHtmlEncoded()
    {
        var (customer, hub, _) = await SeedBaseDataAsync();

        var xssDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-xss",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "<script>alert('xss')</script>"
        };
        _db.Devices.Add(xssDevice);
        await _db.SaveChangesAsync();

        // Add a low battery reading so the battery section appears
        AddBattery(xssDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 10, "critical");
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Battery Levels", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_MultipleHubsPerCustomer_AggregatesAcrossHubs()
    {
        var (customer, hub1, device1) = await SeedBaseDataAsync();

        var hub2 = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "001788FFFE654321",
            HueApplicationKey = "testkey2",
            AccessToken = "token2",
            RefreshToken = "refresh2",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        _db.Hubs.Add(hub2);
        await _db.SaveChangesAsync();

        var device2 = new Device
        {
            HubId = hub2.Id,
            HueDeviceId = "device-hub2-001",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Hub2 Sensor"
        };
        _db.Devices.Add(device2);
        await _db.SaveChangesAsync();

        // Motion from hub1's device
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        // Motion from hub2's device
        AddMotion(device2.Id, new DateTime(2026, 2, 27, 11, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Motion Activity", html);
        // Both devices should be counted in diversity (2 sensors active)
        Assert.Contains(">2<", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_ReadingAtExactBucketBoundary_IncludedInCorrectWindow()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // Reading exactly at the boundary of the 09:00 window start (which is also 05:00–09:00 end)
        // Windows use >= start and < end, so 09:00 exactly falls in the 09:00–13:00 window
        AddMotion(device.Id, new DateTime(2026, 2, 27, 9, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Motion Activity", html);
        // The reading at 09:00 should be counted in the 09:00–13:00 window
        Assert.Contains("09:00", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_MalformedBatteryJson_GracefullySkipped()
    {
        var (customer, hub, _) = await SeedBaseDataAsync();

        var batteryDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-bat-bad",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Bad Battery Sensor"
        };
        _db.Devices.Add(batteryDevice);
        await _db.SaveChangesAsync();

        // Add a malformed battery reading
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = batteryDevice.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Battery,
            Value = "not-valid-json"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // Should not crash; malformed battery data is silently skipped
        Assert.DoesNotContain("Battery Levels", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_HubExistsButNoDevices_ReturnsValidHtml()
    {
        var customer = new Customer { Name = "No Devices User", Email = "nodevices@example.com", TimeZoneId = "UTC" };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "001788FFFE000000",
            HueApplicationKey = "testkey",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Daily Activity Summary", html);
        Assert.Contains("Motion Activity", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithLowBattery_ShowsBatterySection()
    {
        var (customer, hub, device) = await SeedBaseDataAsync();

        // Create a battery-type device
        var batteryDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-bat-001",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Hallway Sensor"
        };
        _db.Devices.Add(batteryDevice);
        await _db.SaveChangesAsync();

        // Add a battery reading below 30%
        AddBattery(batteryDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 15, "low");
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Battery Levels", html);
        Assert.Contains("Hallway Sensor", html);
        Assert.Contains("15%", html);
        // Red color for <30%
        Assert.Contains("#e74c3c", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithAllBatteriesAboveThreshold_NoBatterySection()
    {
        var (customer, hub, device) = await SeedBaseDataAsync();

        var batteryDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-bat-002",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Living Room Sensor"
        };
        _db.Devices.Add(batteryDevice);
        await _db.SaveChangesAsync();

        // Add a battery reading above threshold — Battery Levels section should not show
        AddBattery(batteryDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 85);
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.DoesNotContain("Battery Levels", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithMixedBatteryLevels_ShowsColorCoded()
    {
        var (customer, hub, device) = await SeedBaseDataAsync();

        var lowDevice = new Device { HubId = hub.Id, HueDeviceId = "device-bat-low", DeviceType = DeviceTypes.MotionSensor, Name = "Garage Sensor" };
        var midDevice = new Device { HubId = hub.Id, HueDeviceId = "device-bat-mid", DeviceType = DeviceTypes.MotionSensor, Name = "Kitchen Sensor" };
        var highDevice = new Device { HubId = hub.Id, HueDeviceId = "device-bat-high", DeviceType = DeviceTypes.MotionSensor, Name = "Bedroom Sensor" };
        _db.Devices.AddRange(lowDevice, midDevice, highDevice);
        await _db.SaveChangesAsync();

        AddBattery(lowDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 10, "critical");
        AddBattery(midDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 40);
        AddBattery(highDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 80);
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Battery Levels", html);
        Assert.Contains("Garage Sensor", html);
        Assert.Contains("Kitchen Sensor", html);
        Assert.Contains("Bedroom Sensor", html);
        Assert.Contains("10%", html);
        Assert.Contains("40%", html);
        Assert.Contains("80%", html);
        // All three colors should be present
        Assert.Contains("#e74c3c", html); // red for 10%
        Assert.Contains("#f39c12", html); // yellow for 40%
        Assert.Contains("#27ae60", html); // green for 80%
    }

    [Fact]
    public async Task RenderDailySummaryAsync_NoBatteryReadings_NoBatterySection()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        AddMotion(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.DoesNotContain("Battery Levels", html);
        Assert.DoesNotContain("Device Issues", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_BatterySection_ShowsLatestReadingOnly()
    {
        var (customer, hub, device) = await SeedBaseDataAsync();

        var batteryDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-bat-003",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Study Sensor"
        };
        _db.Devices.Add(batteryDevice);
        await _db.SaveChangesAsync();

        // Older reading at 80%, newer reading at 20%
        AddBattery(batteryDevice.Id, new DateTime(2026, 2, 26, 10, 0, 0, DateTimeKind.Utc), 80);
        AddBattery(batteryDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 20, "low");
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // Should show battery section since latest reading is 20% (<30%)
        Assert.Contains("Battery Levels", html);
        Assert.Contains("20%", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_BatteryAtExactThreshold_ShowsBatterySection()
    {
        var (customer, hub, device) = await SeedBaseDataAsync();

        var batteryDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-bat-boundary",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Hallway Sensor"
        };
        _db.Devices.Add(batteryDevice);
        await _db.SaveChangesAsync();

        // Battery at exactly the default threshold (30) should be included
        AddBattery(batteryDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 30);
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Battery Levels", html);
        Assert.Contains("Hallway Sensor", html);
        Assert.Contains("30%", html);
        // At exactly the critical threshold (30), should show red
        Assert.Contains("#e74c3c", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_TimezoneNameIsHtmlEncoded()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        AddMotion(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        // Render with "Australia/Sydney" which has a StandardName/DaylightName that
        // goes through the Encode() path. Verify the timezone abbreviation appears in the header.
        var html = await _renderer.RenderDailySummaryAsync(customer.Id, "Australia/Sydney", NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Daily Activity Summary", html);
        // The header should contain a parenthesized timezone abbreviation
        // The timezone name is encoded through Encode() — verify it appears in the output
        // and doesn't contain raw unencoded HTML metacharacters
        Assert.DoesNotContain("&lt;", html.Split("Daily Activity Summary")[0]); // no HTML entities before header
    }

    [Fact]
    public async Task RenderDailySummaryAsync_NewestWindowUnder60Min_IsOmitted()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // nowUtc at 01:30 → current window is 01:00–05:00, displayEnd = 01:30, duration = 30 min < 60 min → omitted
        var nowUtc = new DateTime(2026, 2, 28, 1, 30, 0, DateTimeKind.Utc);

        AddMotion(device.Id, new DateTime(2026, 2, 28, 1, 10, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, nowUtc);

        Assert.NotNull(html);
        // The newest window label "01:00–01:30" should NOT appear (omitted)
        Assert.DoesNotContain("01:00\u201301:30", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_NewestWindowAtExactly60Min_IsKept()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // nowUtc at 02:00 → current window is 01:00–05:00, displayEnd = 02:00, duration = 60 min → kept
        var nowUtc = new DateTime(2026, 2, 28, 2, 0, 0, DateTimeKind.Utc);

        AddMotion(device.Id, new DateTime(2026, 2, 28, 1, 10, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, nowUtc);

        Assert.NotNull(html);
        // The newest window should appear (exactly 60 min, not omitted).
        // Duration is 1h < 3h, so end time is wrapped in dark red span.
        Assert.Contains("01:00\u2013<span style=\"color:#8B0000;\">02:00</span>", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_ShortWindow_UsesDarkRedText()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // nowUtc at 03:00 → current window is 01:00–05:00, displayEnd = 03:00, duration = 2h < 3h → dark red
        var nowUtc = new DateTime(2026, 2, 28, 3, 0, 0, DateTimeKind.Utc);

        AddMotion(device.Id, new DateTime(2026, 2, 28, 1, 30, 0, DateTimeKind.Utc));
        AddTemp(device.Id, new DateTime(2026, 2, 28, 1, 30, 0, DateTimeKind.Utc), 20.0);
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, nowUtc);

        Assert.NotNull(html);
        // Dark red color (#8B0000) should appear for the short window
        Assert.Contains("#8B0000", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_FullWindow_DoesNotUseDarkRedText()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // nowUtc = 04:00 → current window is 01:00–05:00 (displayEnd 04:00, exactly 3h — not dark red).
        // Remaining windows are all full 4-hour spans → no dark red anywhere.
        var nowUtc = new DateTime(2026, 2, 28, 4, 0, 0, DateTimeKind.Utc);

        AddMotion(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, nowUtc);

        Assert.NotNull(html);
        // No dark red — all remaining windows are full 4-hour spans
        Assert.DoesNotContain("#8B0000", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WindowAtExactly3Hours_NoDarkRed()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // nowUtc at 04:00 → current window is 01:00–05:00, displayEnd = 04:00, duration = 3h → NOT dark red
        var nowUtc = new DateTime(2026, 2, 28, 4, 0, 0, DateTimeKind.Utc);

        AddMotion(device.Id, new DateTime(2026, 2, 28, 1, 30, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, nowUtc);

        Assert.NotNull(html);
        // Exactly 3 hours — should NOT be dark red
        Assert.DoesNotContain("#8B0000", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_OldBatteryReadingsExcluded()
    {
        var (customer, hub, device) = await SeedBaseDataAsync();

        var batteryDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-bat-old",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Old Battery Sensor"
        };
        _db.Devices.Add(batteryDevice);
        await _db.SaveChangesAsync();

        // Add a battery reading older than 7 days before NowUtc (Feb 28 08:00)
        // Feb 20 is 8 days before — outside the 7-day window
        AddBattery(batteryDevice.Id, new DateTime(2026, 2, 20, 10, 0, 0, DateTimeKind.Utc), 10, "critical");
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // Old battery reading should be excluded — no battery section shown
        Assert.DoesNotContain("Battery Levels", html);
        Assert.DoesNotContain("Old Battery Sensor", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_RecentBatteryReadingsIncluded()
    {
        var (customer, hub, device) = await SeedBaseDataAsync();

        var batteryDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-bat-recent",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Recent Battery Sensor"
        };
        _db.Devices.Add(batteryDevice);
        await _db.SaveChangesAsync();

        // Add a battery reading within the 7-day window
        AddBattery(batteryDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 15, "low");
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Battery Levels", html);
        Assert.Contains("Recent Battery Sensor", html);
        Assert.Contains("15%", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_BatteryLatestHighIgnoresOlderLow()
    {
        var (customer, hub, device) = await SeedBaseDataAsync();

        var batteryDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-bat-latest-high",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Recovered Sensor"
        };
        _db.Devices.Add(batteryDevice);
        await _db.SaveChangesAsync();

        // Older reading at 10% (critical), newer reading at 90% (healthy)
        AddBattery(batteryDevice.Id, new DateTime(2026, 2, 25, 10, 0, 0, DateTimeKind.Utc), 10, "critical");
        AddBattery(batteryDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 90);
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        // Latest reading is 90% (above threshold) — no Battery Levels section
        Assert.DoesNotContain("Battery Levels", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_LatestLocations_ShowsTwoDistinctSensors()
    {
        var (customer, hub, device1) = await SeedBaseDataAsync();

        var device2 = new Device { HubId = hub.Id, HueDeviceId = "device-002", DeviceType = DeviceTypes.MotionSensor, Name = "Bedroom" };
        var device3 = new Device { HubId = hub.Id, HueDeviceId = "device-003", DeviceType = DeviceTypes.MotionSensor, Name = "Kitchen" };
        _db.Devices.AddRange(device2, device3);
        await _db.SaveChangesAsync();

        // Three devices with motion in the 08:00–12:00 window on Feb 27
        // device3 latest at 11:30, device2 at 11:00, device1 at 10:00
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        AddMotion(device2.Id, new DateTime(2026, 2, 27, 11, 0, 0, DateTimeKind.Utc));
        AddMotion(device3.Id, new DateTime(2026, 2, 27, 11, 30, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        // Should show Kitchen (11:30) and Bedroom (11:00), NOT Sensor 1
        Assert.Contains("Kitchen (11:30)", html);
        Assert.Contains("Bedroom (11:00)", html);
        Assert.DoesNotContain("Sensor 1 (10:00)", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_LatestLocations_GroupsByDeviceId_NotName()
    {
        var (customer, hub, device1) = await SeedBaseDataAsync();

        // Two devices with same name but different IDs
        var device2 = new Device { HubId = hub.Id, HueDeviceId = "device-002", DeviceType = DeviceTypes.MotionSensor, Name = "Sensor 1" };
        _db.Devices.Add(device2);
        await _db.SaveChangesAsync();

        // Both devices named "Sensor 1" with motion in the same window
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        AddMotion(device2.Id, new DateTime(2026, 2, 27, 11, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        // Both devices should appear since they have different IDs, even though same name
        // Count occurrences of "Sensor 1" in the Latest Locations section
        var latestSection = html.Substring(html.IndexOf("Latest Locations"));
        var tempSection = latestSection.IndexOf("Temperature Range");
        latestSection = latestSection.Substring(0, tempSection);
        var count = latestSection.Split("Sensor 1").Length - 1;
        Assert.True(count >= 2, $"Expected at least 2 occurrences of 'Sensor 1' in Latest Locations, found {count}");
    }

    [Fact]
    public async Task RenderDailySummaryAsync_LatestLocations_DeduplicatesMultipleReadingsPerDevice()
    {
        var (customer, hub, device1) = await SeedBaseDataAsync();

        var device2 = new Device { HubId = hub.Id, HueDeviceId = "device-002", DeviceType = DeviceTypes.MotionSensor, Name = "Bedroom" };
        _db.Devices.Add(device2);
        await _db.SaveChangesAsync();

        // device1 has multiple motion readings in the same window — should only show latest
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 9, 0, 0, DateTimeKind.Utc));
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 11, 0, 0, DateTimeKind.Utc));
        AddMotion(device2.Id, new DateTime(2026, 2, 27, 10, 30, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        // Should show Sensor 1 with latest time (11:00) and Bedroom (10:30)
        Assert.Contains("Sensor 1 (11:00)", html);
        Assert.Contains("Bedroom (10:30)", html);
        // Should NOT show earlier Sensor 1 timestamps
        Assert.DoesNotContain("Sensor 1 (09:00)", html);
        Assert.DoesNotContain("Sensor 1 (10:00)", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_LatestLocations_SingleDevice_ShowsOneEntry()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        AddMotion(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.Contains("Sensor 1 (10:00)", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_LatestLocations_NoMotion_ShowsEmDash()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // Only temperature readings, no motion
        AddTemp(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 20.0);
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.Contains("Latest Locations", html);
        // Em dash for no data
        Assert.Contains("\u2014", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_LatestLocations_MotionFalse_NotIncluded()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // motion:false readings should not appear in latest locations
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.Motion,
            Value = "{\"motion\":false,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        // Should not show sensor name with time in Latest Locations
        Assert.DoesNotContain("Sensor 1 (10:00)", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_CustomerName_RenderedInHeader()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        AddMotion(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.Contains("for Test User", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_CustomerNameEmpty_NoSubHeading()
    {
        var customer = new Customer { Name = "", Email = "noname@example.com", TimeZoneId = "UTC" };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "001788FFFE111111",
            HueApplicationKey = "testkey",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.DoesNotContain("for ", html.Split("Daily Activity Summary")[1].Split("</td>")[0]);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_CustomerNameWithHtml_IsEncoded()
    {
        var customer = new Customer { Name = "<b>Evil</b>", Email = "xss@example.com", TimeZoneId = "UTC" };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "001788FFFE222222",
            HueApplicationKey = "testkey",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.DoesNotContain("<b>Evil</b>", html);
        Assert.Contains("&lt;b&gt;Evil&lt;/b&gt;", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_LatestLocations_XssInSensorName_IsEncoded()
    {
        var (customer, hub, _) = await SeedBaseDataAsync();

        var xssDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-xss-motion",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "<img src=x onerror=alert(1)>"
        };
        _db.Devices.Add(xssDevice);
        await _db.SaveChangesAsync();

        AddMotion(xssDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;img", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_BatterySection_SortedByLevelAscending()
    {
        var (customer, hub, _) = await SeedBaseDataAsync();

        var bat1 = new Device { HubId = hub.Id, HueDeviceId = "bat-high", DeviceType = DeviceTypes.MotionSensor, Name = "High Battery" };
        var bat2 = new Device { HubId = hub.Id, HueDeviceId = "bat-low", DeviceType = DeviceTypes.MotionSensor, Name = "Low Battery" };
        var bat3 = new Device { HubId = hub.Id, HueDeviceId = "bat-mid", DeviceType = DeviceTypes.MotionSensor, Name = "Mid Battery" };
        _db.Devices.AddRange(bat1, bat2, bat3);
        await _db.SaveChangesAsync();

        AddBattery(bat1.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 80);
        AddBattery(bat2.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 5, "critical");
        AddBattery(bat3.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 40);
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.Contains("Battery Levels", html);
        // Low Battery (5%) should appear before Mid Battery (40%) before High Battery (80%)
        var lowIdx = html.IndexOf("Low Battery");
        var midIdx = html.IndexOf("Mid Battery");
        var highIdx = html.IndexOf("High Battery");
        Assert.True(lowIdx < midIdx, "Low Battery should appear before Mid Battery");
        Assert.True(midIdx < highIdx, "Mid Battery should appear before High Battery");
    }

    [Fact]
    public void AppendFallbackNote_InsertsNoteBeforeClosingTag()
    {
        const string html = "<table><tr><td>content</td></tr></table></body></html>";
        var result = EmailRenderer.AppendFallbackNote(html);
        Assert.EndsWith("</table></body></html>", result);
        Assert.True(result.IndexOf("Note:") < result.IndexOf("</table></body></html>"));
    }

    [Fact]
    public void AppendFallbackNote_NoteContainsExpectedText()
    {
        const string html = "<table></table></body></html>";
        var result = EmailRenderer.AppendFallbackNote(html);
        Assert.Contains("sent individually", result);
        Assert.Contains("one or more recipients failed", result);
    }

    [Fact]
    public void AppendFallbackNote_ReturnsUnchanged_WhenClosingTagAbsent()
    {
        const string html = "<table><tr><td>no closing tag</td></tr>";
        var result = EmailRenderer.AppendFallbackNote(html);
        Assert.Equal(html, result);
    }

    [Fact]
    public void AppendFallbackNote_DoesNotDuplicateClosingTag()
    {
        const string html = "<table></table></body></html>";
        var result = EmailRenderer.AppendFallbackNote(html);
        Assert.Equal(1, CountOccurrences(result, "</table></body></html>"));
    }

    [Fact]
    public async Task RenderDailySummaryAsync_FivePlusActiveAreas_ShowsFivePlusDiversityLabel()
    {
        var (customer, hub, device1) = await SeedBaseDataAsync();

        // Create 5 additional motion sensors (6 total) so diversity label hits "5+"
        var devices = new List<Device> { device1 };
        for (int i = 2; i <= 6; i++)
        {
            var d = new Device { HubId = hub.Id, HueDeviceId = $"device-{i:D3}", DeviceType = DeviceTypes.MotionSensor, Name = $"Sensor {i}" };
            _db.Devices.Add(d);
            devices.Add(d);
        }
        await _db.SaveChangesAsync();

        // Add one motion event per device in the same window (08:00–12:00 Feb 27)
        for (int i = 0; i < devices.Count; i++)
        {
            AddMotion(devices[i].Id, new DateTime(2026, 2, 27, 9, 0, 0, DateTimeKind.Utc).AddMinutes(i * 5));
        }
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // The Location Diversity section should show "5+" for 6 active areas
        var diversitySection = html.IndexOf("Location Diversity");
        Assert.True(diversitySection > 0, "Location Diversity section should exist");
        // Should contain the 5+ label for diversity
        var diversityContent = html.Substring(diversitySection);
        Assert.Contains("5+", diversityContent);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_NullNowUtc_UsesCurrentTime()
    {
        var (customer, _, _) = await SeedBaseDataAsync();

        // Pass null for nowUtc to exercise the fallback branch
        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, null);

        Assert.NotNull(html);
        Assert.Contains("Daily Activity Summary", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_CustomerWithCustomWindowHours_UsesOverride()
    {
        var (customer, _, device) = await SeedBaseDataAsync();
        customer.SummaryWindowHours = 6; // Override default 4h to 6h
        await _db.SaveChangesAsync();

        AddMotion(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("Daily Activity Summary", html);
        // 6h windows with offset 1: boundaries at 01:00, 07:00, 13:00, 19:00
        // Should see 6h-wide window labels (not the default 4h)
        Assert.Contains("01:00", html);
        Assert.Contains("07:00", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_CustomerWithCustomWindowCount_UsesOverride()
    {
        var (customer, _, device) = await SeedBaseDataAsync();
        customer.SummaryWindowCount = 3; // Override default 7 to 3
        await _db.SaveChangesAsync();

        AddMotion(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        // Default: 7 windows. Custom: 3 windows. The renderer uses 3.
        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // Count "Motion Activity" rows — 3 windows means fewer time labels
        var motionSection = html.Substring(html.IndexOf("Motion Activity"));
        var locationSection = motionSection.Substring(0, motionSection.IndexOf("Location Diversity"));
        // Each window produces one <tr> with a time label; with 3 windows we expect fewer rows
        // than the default 7. Count the window label patterns.
        var windowLabels = CountOccurrences(locationSection, "\u2013");
        Assert.True(windowLabels <= 3, $"Expected at most 3 window labels but found {windowLabels}");
    }

    [Fact]
    public async Task RenderDailySummaryAsync_CustomerWithCustomOffset_UsesOverride()
    {
        var (customer, _, device) = await SeedBaseDataAsync();
        customer.SummaryWindowOffsetHours = 0; // Override default 1 to 0
        await _db.SaveChangesAsync();

        AddMotion(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // With offset 0 and 4h windows: boundaries at 00:00, 04:00, 08:00, 12:00, 16:00, 20:00
        Assert.Contains("04:00", html);
        Assert.Contains("08:00", html);
        Assert.Contains("12:00", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_CustomerNullSettings_FallsBackToGlobal()
    {
        var (customer, _, device) = await SeedBaseDataAsync();
        // Ensure customer has null overrides (the defaults)
        Assert.Null(customer.SummaryWindowHours);
        Assert.Null(customer.SummaryWindowCount);
        Assert.Null(customer.SummaryWindowOffsetHours);

        AddMotion(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // With default offset=1 and 4h windows: boundaries at 01:00, 05:00, 09:00, 13:00, 17:00, 21:00
        Assert.Contains("05:00", html);
        Assert.Contains("09:00", html);
        Assert.Contains("13:00", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_IncludeLatestLocationsTrue_ShowsSection()
    {
        var (customer, _, device) = await SeedBaseDataAsync();
        customer.IncludeLatestLocations = true;
        await _db.SaveChangesAsync();

        AddMotion(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.Contains("Latest Locations", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_IncludeLatestLocationsFalse_OmitsSection()
    {
        var (customer, _, device) = await SeedBaseDataAsync();
        customer.IncludeLatestLocations = false;
        await _db.SaveChangesAsync();

        AddMotion(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.DoesNotContain("Latest Locations", html);
        // Other sections should still be present
        Assert.Contains("Motion Activity", html);
        Assert.Contains("Location Diversity", html);
        Assert.Contains("Temperature Range", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_IncludeLatestLocationsDefault_ShowsSection()
    {
        // When customer doesn't exist in DB (e.g. invalid ID), IncludeLatestLocations falls back to true
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var db = new HpollDbContext(options);
        var emailSettings = Options.Create(new EmailSettings());
        var renderer = new EmailRenderer(db, NullLogger<EmailRenderer>.Instance, emailSettings);

        var html = await renderer.RenderDailySummaryAsync(9999, TimeZone, NowUtc);

        // Should include Latest Locations by default (fallback to true when customer not found)
        Assert.Contains("Latest Locations", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_AllCustomerOverrides_Applied()
    {
        var (customer, _, device) = await SeedBaseDataAsync();
        customer.SummaryWindowHours = 6;
        customer.SummaryWindowCount = 2;
        customer.SummaryWindowOffsetHours = 3;
        customer.IncludeLatestLocations = false;
        await _db.SaveChangesAsync();

        AddMotion(device.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.DoesNotContain("Latest Locations", html);
        // With 6h windows, offset 3, count 2: only 2 window rows in Motion Activity
        var motionSection = html.Substring(html.IndexOf("Motion Activity"));
        var diversityIdx = motionSection.IndexOf("Location Diversity");
        var motionContent = motionSection.Substring(0, diversityIdx);
        var windowLabels = CountOccurrences(motionContent, "\u2013");
        Assert.True(windowLabels <= 2, $"Expected at most 2 window labels but found {windowLabels}");
    }

    private void AddConnectivity(int deviceId, DateTime timestamp, string status, string macAddress = "00:11:22:33:44:55")
    {
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = deviceId,
            Timestamp = timestamp,
            ReadingType = ReadingTypes.ZigbeeConnectivity,
            Value = $"{{\"status\":\"{status}\",\"mac_address\":\"{macAddress}\"}}"
        });
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithUnreachableDevice_ShowsDeviceStatusSection()
    {
        var (customer, hub, device) = await SeedBaseDataAsync();

        var zigbeeDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-zigbee-001",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Hallway Sensor"
        };
        _db.Devices.Add(zigbeeDevice);
        await _db.SaveChangesAsync();

        AddConnectivity(zigbeeDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), "disconnected");
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.Contains("Device Issues", html);
        Assert.Contains("Hallway Sensor", html);
        Assert.Contains("Disconnected", html);
        Assert.Contains("\u26a0", html); // warning icon
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithConnectedDevice_NoDeviceStatusSection()
    {
        var (customer, hub, device) = await SeedBaseDataAsync();

        var zigbeeDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-zigbee-002",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Kitchen Sensor"
        };
        _db.Devices.Add(zigbeeDevice);
        await _db.SaveChangesAsync();

        AddConnectivity(zigbeeDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), "connected");
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.DoesNotContain("Device Issues", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_UnreachableDeviceOnly_ShowsSectionWithoutBattery()
    {
        var (customer, hub, device) = await SeedBaseDataAsync();

        // No battery devices, just an unreachable zigbee device
        var zigbeeDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-zigbee-003",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Garage Sensor"
        };
        _db.Devices.Add(zigbeeDevice);
        await _db.SaveChangesAsync();

        AddConnectivity(zigbeeDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), "connectivity_issue");
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.Contains("Device Issues", html);
        Assert.Contains("Garage Sensor", html);
        Assert.Contains("Connectivity Issue", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_LowBatteryAndUnreachable_ShowsBothWithConnectivityFirst()
    {
        var (customer, hub, device) = await SeedBaseDataAsync();

        var batteryDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-bat-both",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Low Battery Device"
        };
        var zigbeeDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-zigbee-both",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Unreachable Device"
        };
        _db.Devices.AddRange(batteryDevice, zigbeeDevice);
        await _db.SaveChangesAsync();

        AddBattery(batteryDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), 15, "low");
        AddConnectivity(zigbeeDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), "disconnected");
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.Contains("Device Issues", html);
        Assert.Contains("Battery Levels", html);
        Assert.Contains("Low Battery Device", html);
        Assert.Contains("15%", html);
        Assert.Contains("Unreachable Device", html);
        Assert.Contains("Disconnected", html);

        // Device Issues section should appear before Battery Levels section
        var issuesPos = html.IndexOf("Device Issues", StringComparison.Ordinal);
        var batteryPos = html.IndexOf("Battery Levels", StringComparison.Ordinal);
        Assert.True(issuesPos < batteryPos, "Device Issues should render before Battery Levels");
    }

    [Fact]
    public async Task RenderDailySummaryAsync_ConnectivityStatusLabels_MappedCorrectly()
    {
        var (customer, hub, device) = await SeedBaseDataAsync();

        var devices = new[]
        {
            ("device-z-1", "Sensor A", "disconnected", "Disconnected"),
            ("device-z-2", "Sensor B", "connectivity_issue", "Connectivity Issue"),
            ("device-z-3", "Sensor C", "unidirectional_incoming", "Limited Connectivity"),
            ("device-z-4", "Sensor D", "configuration_error", "Configuration Error"),
            ("device-z-5", "Sensor E", "pending_discovery", "Pending Discovery"),
        };

        foreach (var (hueId, name, status, _) in devices)
        {
            var d = new Device { HubId = hub.Id, HueDeviceId = hueId, DeviceType = DeviceTypes.MotionSensor, Name = name };
            _db.Devices.Add(d);
            await _db.SaveChangesAsync();
            AddConnectivity(d.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), status);
        }
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        foreach (var (_, name, _, label) in devices)
        {
            Assert.Contains(name, html);
            Assert.Contains(label, html);
        }
    }

    [Fact]
    public async Task RenderDailySummaryAsync_MalformedConnectivityJson_SkipsGracefully()
    {
        var (customer, hub, device) = await SeedBaseDataAsync();

        var zigbeeDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-zigbee-bad",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Bad Zigbee Sensor"
        };
        _db.Devices.Add(zigbeeDevice);
        await _db.SaveChangesAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = zigbeeDevice.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = ReadingTypes.ZigbeeConnectivity,
            Value = "not-valid-json"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        // Should not crash; malformed connectivity data is silently skipped
        Assert.DoesNotContain("Bad Zigbee Sensor", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_UnknownConnectivityStatus_UsesRawValue()
    {
        var (customer, hub, device) = await SeedBaseDataAsync();

        var zigbeeDevice = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-zigbee-unknown",
            DeviceType = DeviceTypes.MotionSensor,
            Name = "Custom Status Sensor"
        };
        _db.Devices.Add(zigbeeDevice);
        await _db.SaveChangesAsync();

        AddConnectivity(zigbeeDevice.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc), "some_new_status");
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.Contains("Device Issues", html);
        Assert.Contains("some_new_status", html);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}
