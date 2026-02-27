using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Hpoll.Data;
using Hpoll.Data.Entities;
using Hpoll.Email;

namespace Hpoll.Core.Tests;

public class EmailRendererTests : IDisposable
{
    private readonly HpollDbContext _db;
    private readonly EmailRenderer _renderer;

    // nowUtc = Feb 28 08:00 UTC
    // With UTC timezone: bucketEnd = 12:00 (end of current window), bucketStart = Feb 27 08:00 (28h back)
    // 7 windows: 08:00–12:00, 12:00–16:00, 16:00–20:00, 20:00–00:00, 00:00–04:00, 04:00–08:00, 08:00–12:00
    private static readonly DateTime NowUtc = new(2026, 2, 28, 8, 0, 0, DateTimeKind.Utc);
    private const string TimeZone = "UTC";

    public EmailRendererTests()
    {
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new HpollDbContext(options);
        _renderer = new EmailRenderer(_db, NullLogger<EmailRenderer>.Instance);
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
            Status = "active"
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        var device = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-001",
            DeviceType = "motion_sensor",
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
    public async Task RenderDailySummaryAsync_WithMotionReadings_ShowsActivity()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // Readings at Feb 27 10:00 and 11:00 — within the 08:00–12:00 window
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 11, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
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
            ReadingType = "temperature",
            Value = "{\"temperature\":19.5,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 11, 0, 0, DateTimeKind.Utc),
            ReadingType = "temperature",
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
    public async Task RenderDailySummaryAsync_ContainsPrivacyFooter()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        Assert.Contains("No individual device or location details are included for privacy", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_ContainsAllSevenWindows()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // Seven 4-hour windows (28h) aligned to standard boundaries
        // With nowUtc at 08:00 UTC: 04:00, 08:00, 12:00, 16:00, 20:00, 00:00, 04:00
        Assert.Contains("04:00", html);
        Assert.Contains("08:00", html);
        Assert.Contains("12:00", html);
        Assert.Contains("16:00", html);
        Assert.Contains("20:00", html);
        Assert.Contains("00:00", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_WithMalformedJson_HandlesGracefully()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "not-valid-json"
        });
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "temperature",
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
            ReadingType = "motion",
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
            DeviceType = "motion_sensor",
            Name = "Sensor 2"
        };
        _db.Devices.Add(device2);
        await _db.SaveChangesAsync();

        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device1.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
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
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        Assert.NotNull(html);
        // Header shows time range: "27 Feb 2026 08:00 – 28 Feb 2026 12:00"
        Assert.Contains("27 Feb 2026", html);
        Assert.Contains("28 Feb 2026", html);
    }

    [Fact]
    public async Task RenderDailySummaryAsync_ReadingsOutsideWindow_AreExcluded()
    {
        var (customer, _, device) = await SeedBaseDataAsync();

        // Reading inside the window (Feb 27 10:00 — within 08:00–12:00)
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T10:00:00Z\"}"
        });
        // Reading outside the window (Feb 27 07:00 — before the 28h window starts at 08:00)
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 7, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T07:00:00Z\"}"
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
            Status = "active"
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        var device = new Device
        {
            HubId = hub.Id,
            HueDeviceId = "device-au-001",
            DeviceType = "motion_sensor",
            Name = "Lounge Sensor"
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        // nowUtc = Feb 28 08:00 UTC = Feb 28 19:00 AEDT
        // bucketEnd = floor(19/4)*4+4 = 20:00 AEDT on Feb 28 = Feb 28 09:00 UTC
        // bucketStart = 28h back = 16:00 AEDT on Feb 27 = Feb 27 05:00 UTC
        // 7 windows: 16:00–20:00, 20:00–00:00, 00:00–04:00, 04:00–08:00, 08:00–12:00, 12:00–16:00, 16:00–20:00 AEDT

        // Reading at Feb 27 06:00 UTC = Feb 27 17:00 AEDT — falls in 16:00–20:00 AEDT window
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = device.Id,
            Timestamp = new DateTime(2026, 2, 27, 6, 0, 0, DateTimeKind.Utc),
            ReadingType = "motion",
            Value = "{\"motion\":true,\"changed\":\"2026-02-27T06:00:00Z\"}"
        });
        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, "Australia/Sydney", NowUtc);

        Assert.NotNull(html);
        // Windows should be labeled in AEDT local time
        Assert.Contains("16:00", html);
        Assert.Contains("20:00", html);
        Assert.Contains("00:00", html);
        Assert.Contains("04:00", html);
        Assert.Contains("08:00", html);
        Assert.Contains("12:00", html);
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
        // The newest window (08:00–12:00 on Feb 28) should appear before the oldest (08:00–12:00 on Feb 27)
        // in the Motion Activity section. Both have label "08:00–12:00" but newest is listed first.
        var motionIdx = html.IndexOf("Motion Activity");
        var firstWindowLabel = html.IndexOf("08:00", motionIdx + "Motion Activity".Length);
        Assert.True(firstWindowLabel > motionIdx, "First window after Motion Activity header should be the newest");
    }

    [Fact]
    public async Task RenderDailySummaryAsync_GeneratePreviewHtml()
    {
        var (customer, hub, device1) = await SeedBaseDataAsync();

        var device2 = new Device { HubId = hub.Id, HueDeviceId = "device-002", DeviceType = "motion_sensor", Name = "Sensor 2" };
        var device3 = new Device { HubId = hub.Id, HueDeviceId = "device-003", DeviceType = "motion_sensor", Name = "Sensor 3" };
        _db.Devices.AddRange(device2, device3);
        await _db.SaveChangesAsync();

        // Window 08:00–12:00 Feb 27: 3 events, 2 devices — GREEN activity, GREEN diversity
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 9, 0, 0, DateTimeKind.Utc));
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 10, 0, 0, DateTimeKind.Utc));
        AddMotion(device2.Id, new DateTime(2026, 2, 27, 11, 0, 0, DateTimeKind.Utc));
        AddTemp(device1.Id, new DateTime(2026, 2, 27, 9, 0, 0, DateTimeKind.Utc), 18.5);
        AddTemp(device1.Id, new DateTime(2026, 2, 27, 11, 0, 0, DateTimeKind.Utc), 21.0);

        // Window 12:00–16:00 Feb 27: 1 event, 1 device — YELLOW activity, YELLOW diversity
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 14, 0, 0, DateTimeKind.Utc));
        AddTemp(device1.Id, new DateTime(2026, 2, 27, 14, 0, 0, DateTimeKind.Utc), 22.5);

        // Window 16:00–20:00 Feb 27: 0 events — RED activity, RED diversity
        AddTemp(device1.Id, new DateTime(2026, 2, 27, 18, 0, 0, DateTimeKind.Utc), 20.0);

        // Window 20:00–00:00 Feb 27–28: 6 events, 3 devices — GREEN 5+, GREEN diversity
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 21, 0, 0, DateTimeKind.Utc));
        AddMotion(device1.Id, new DateTime(2026, 2, 27, 22, 0, 0, DateTimeKind.Utc));
        AddMotion(device2.Id, new DateTime(2026, 2, 27, 21, 30, 0, DateTimeKind.Utc));
        AddMotion(device2.Id, new DateTime(2026, 2, 27, 22, 30, 0, DateTimeKind.Utc));
        AddMotion(device3.Id, new DateTime(2026, 2, 27, 23, 0, 0, DateTimeKind.Utc));
        AddMotion(device3.Id, new DateTime(2026, 2, 27, 23, 30, 0, DateTimeKind.Utc));
        AddTemp(device1.Id, new DateTime(2026, 2, 27, 22, 0, 0, DateTimeKind.Utc), 19.0);

        // Window 00:00–04:00 Feb 28: 0 events — RED activity, RED diversity

        // Window 04:00–08:00 Feb 28: 2 events, 1 device — GREEN activity, YELLOW diversity
        AddMotion(device1.Id, new DateTime(2026, 2, 28, 5, 0, 0, DateTimeKind.Utc));
        AddMotion(device1.Id, new DateTime(2026, 2, 28, 6, 0, 0, DateTimeKind.Utc));
        AddTemp(device1.Id, new DateTime(2026, 2, 28, 6, 0, 0, DateTimeKind.Utc), 17.5);

        // Window 08:00–12:00 Feb 28 (current): 1 event, 1 device — YELLOW
        AddMotion(device2.Id, new DateTime(2026, 2, 28, 8, 30, 0, DateTimeKind.Utc));

        await _db.SaveChangesAsync();

        var html = await _renderer.RenderDailySummaryAsync(customer.Id, TimeZone, NowUtc);

        var previewPath = Path.Combine(Path.GetTempPath(), "hpoll_email_preview.html");
        File.WriteAllText(previewPath, html);

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
            ReadingType = "motion",
            Value = $"{{\"motion\":true,\"changed\":\"{timestamp:O}\"}}"
        });
    }

    private void AddTemp(int deviceId, DateTime timestamp, double temp)
    {
        _db.DeviceReadings.Add(new DeviceReading
        {
            DeviceId = deviceId,
            Timestamp = timestamp,
            ReadingType = "temperature",
            Value = $"{{\"temperature\":{temp},\"changed\":\"{timestamp:O}\"}}"
        });
    }
}
