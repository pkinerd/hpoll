using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Hpoll.Admin.Pages;
using Hpoll.Core.Configuration;
using Hpoll.Core.Constants;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Tests;

public class AboutModelTests : IDisposable
{
    private readonly HpollDbContext _db;

    public AboutModelTests()
    {
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new HpollDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private static IOptions<HueAppSettings> CreateHueOptions(string? callbackUrl = null, string? clientId = null)
    {
        return Options.Create(new HueAppSettings
        {
            CallbackUrl = callbackUrl ?? string.Empty,
            ClientId = clientId ?? string.Empty
        });
    }

    private AboutModel CreateModel(string? callbackUrl = null, string? clientId = null)
    {
        return new AboutModel(_db, CreateHueOptions(callbackUrl, clientId),
            NullLogger<AboutModel>.Instance);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsCorrectDbCounts()
    {
        var customer = new Customer { Name = "Test", Email = "test@example.com", TimeZoneId = "UTC" };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "001788FFFE123456",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        var device = new Device { HubId = hub.Id, HueDeviceId = "dev-001", DeviceType = DeviceTypes.MotionSensor, Name = "Sensor" };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        var model = CreateModel();
        await model.OnGetAsync();

        Assert.Equal(1, model.CustomerCount);
        Assert.Equal(1, model.HubCount);
        Assert.Equal(1, model.DeviceCount);
    }

    [Fact]
    public async Task OnGetAsync_GroupsSystemInfoByCategory()
    {
        _db.SystemInfo.AddRange(
            new SystemInfo { Key = "system.version", Value = "1.0.0", Category = "System" },
            new SystemInfo { Key = "polling.interval_minutes", Value = "60", Category = "Polling" },
            new SystemInfo { Key = "email.aws_region", Value = "us-east-1", Category = "Email" }
        );
        await _db.SaveChangesAsync();

        var model = CreateModel();
        await model.OnGetAsync();

        Assert.Equal(3, model.Sections.Count);
        Assert.Equal("System", model.Sections[0].Category);
        Assert.Equal("Polling", model.Sections[1].Category);
        Assert.Equal("Email", model.Sections[2].Category);
    }

    [Fact]
    public async Task OnGetAsync_FormatsLabelsCorrectly()
    {
        _db.SystemInfo.Add(new SystemInfo { Key = "polling.interval_minutes", Value = "60", Category = "Polling" });
        await _db.SaveChangesAsync();

        var model = CreateModel();
        await model.OnGetAsync();

        var entry = model.Sections[0].Entries[0];
        Assert.Equal("Interval Minutes", entry.Label);
    }

    [Fact]
    public async Task OnGetAsync_FormatsDateValues()
    {
        _db.SystemInfo.Add(new SystemInfo { Key = "runtime.last_poll_completed", Value = "2026-03-01T10:00:00.0000000Z", Category = "Runtime" });
        await _db.SaveChangesAsync();

        var model = CreateModel();
        await model.OnGetAsync();

        var entry = model.Sections[0].Entries[0];
        Assert.Contains("2026-03-01 10:00:00", entry.Value);
        Assert.Contains("UTC", entry.Value);
    }

    [Fact]
    public async Task OnGetAsync_HandlesEmptySystemInfoTable()
    {
        var model = CreateModel();
        await model.OnGetAsync();

        Assert.Empty(model.Sections);
        Assert.Equal(0, model.CustomerCount);
    }

    [Fact]
    public async Task OnGetAsync_SectionsInExpectedOrder()
    {
        _db.SystemInfo.AddRange(
            new SystemInfo { Key = "build.branch", Value = "main", Category = "Build" },
            new SystemInfo { Key = "runtime.total_poll_cycles", Value = "5", Category = "Runtime" },
            new SystemInfo { Key = "email.aws_region", Value = "us-east-1", Category = "Email" },
            new SystemInfo { Key = "system.version", Value = "1.0.0", Category = "System" },
            new SystemInfo { Key = "polling.interval_minutes", Value = "60", Category = "Polling" },
            new SystemInfo { Key = "hue.app_configured", Value = "True", Category = "Hue" },
            new SystemInfo { Key = "backup.interval_hours", Value = "24", Category = "Backup" }
        );
        await _db.SaveChangesAsync();

        var model = CreateModel();
        await model.OnGetAsync();

        var categories = model.Sections.Select(s => s.Category).ToList();
        Assert.Equal(new[] { "Worker Build", "System", "Polling", "Email", "Hue", "Backup", "Runtime" }, categories);
    }

    [Fact]
    public async Task OnGetAsync_ShowsCallbackUrlFromConfig_WhenWorkerHasNotStarted()
    {
        // No SystemInfo data at all — Worker hasn't started
        var model = CreateModel(
            callbackUrl: "https://admin.example.com/Hubs/OAuthCallback",
            clientId: "test-client-id");
        await model.OnGetAsync();

        Assert.Single(model.Sections);
        Assert.Equal("Hue", model.Sections[0].Category);
        var entries = model.Sections[0].Entries;
        Assert.Contains(entries, e => e.Label == "App Configured" && e.Value == "True");
        Assert.Contains(entries, e => e.Label == "Callback Url" && e.Value == "https://admin.example.com/Hubs/OAuthCallback");
    }

    [Fact]
    public async Task OnGetAsync_ShowsCallbackUrlFromConfig_WhenWorkerHueSection_MissingCallbackUrl()
    {
        // Worker has written app_configured but NOT callback_url
        _db.SystemInfo.Add(new SystemInfo { Key = "hue.app_configured", Value = "True", Category = "Hue" });
        await _db.SaveChangesAsync();

        var model = CreateModel(
            callbackUrl: "https://admin.example.com/Hubs/OAuthCallback");
        await model.OnGetAsync();

        var hueSection = model.Sections.Single(s => s.Category == "Hue");
        Assert.Contains(hueSection.Entries, e => e.Label == "Callback Url" && e.Value == "https://admin.example.com/Hubs/OAuthCallback");
    }

    [Fact]
    public async Task OnGetAsync_DoesNotDuplicateCallbackUrl_WhenWorkerAlreadyWroteIt()
    {
        // Worker has written both app_configured and callback_url
        _db.SystemInfo.AddRange(
            new SystemInfo { Key = "hue.app_configured", Value = "True", Category = "Hue" },
            new SystemInfo { Key = "hue.callback_url", Value = "https://worker.example.com/Hubs/OAuthCallback", Category = "Hue" }
        );
        await _db.SaveChangesAsync();

        var model = CreateModel(
            callbackUrl: "https://admin.example.com/Hubs/OAuthCallback");
        await model.OnGetAsync();

        var hueSection = model.Sections.Single(s => s.Category == "Hue");
        // Should not duplicate — Worker's value is kept
        Assert.Single(hueSection.Entries.Where(e => e.Label == "Callback Url"));
    }

    [Fact]
    public async Task OnGetAsync_ShowsCallbackUrlFromConfig_WhenWorkerWroteEmptyValue()
    {
        // Worker wrote an empty callback_url (Worker config doesn't include CallbackUrl)
        _db.SystemInfo.AddRange(
            new SystemInfo { Key = "hue.app_configured", Value = "True", Category = "Hue" },
            new SystemInfo { Key = "hue.callback_url", Value = "", Category = "Hue" }
        );
        await _db.SaveChangesAsync();

        var model = CreateModel(
            callbackUrl: "https://admin.example.com/Hubs/OAuthCallback");
        await model.OnGetAsync();

        var hueSection = model.Sections.Single(s => s.Category == "Hue");
        Assert.Contains(hueSection.Entries, e => e.Label == "Callback Url" && e.Value == "https://admin.example.com/Hubs/OAuthCallback");
    }

    [Fact]
    public async Task OnGetAsync_NoCallbackUrlEntry_WhenConfigIsEmpty()
    {
        var model = CreateModel(callbackUrl: "");
        await model.OnGetAsync();

        // No Hue section at all since config is empty and Worker hasn't written anything
        Assert.Empty(model.Sections);
    }
}

/// <summary>
/// Tests for OnPostExportSanitizedDbAsync using a file-based SQLite database
/// (VACUUM INTO does not work with in-memory databases).
/// </summary>
public class AboutModelExportTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;
    private readonly HpollDbContext _db;

    public AboutModelExportTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"hpoll-test-{Guid.NewGuid()}.db");
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new HpollDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        try { File.Delete(_dbPath); } catch { }
    }

    private AboutModel CreateModel()
    {
        var hueOptions = Options.Create(new HueAppSettings());
        var model = new AboutModel(_db, hueOptions, NullLogger<AboutModel>.Instance);
        var httpContext = new DefaultHttpContext();
        model.PageContext = new PageContext
        {
            ActionDescriptor = new CompiledPageActionDescriptor(),
            HttpContext = httpContext,
            RouteData = new RouteData()
        };
        model.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        return model;
    }

    [Fact]
    public async Task ExportSanitizedDb_ReturnsSanitizedFile()
    {
        // Seed sensitive data
        var customer = new Customer
        {
            Name = "Test Customer",
            Email = "secret@example.com",
            CcEmails = "cc@example.com",
            BccEmails = "bcc@example.com",
            TimeZoneId = "UTC"
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "001788FFFE123456",
            HueApplicationKey = "secret-app-key",
            AccessToken = "secret-access-token",
            RefreshToken = "secret-refresh-token",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = HubStatus.Active
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        var model = CreateModel();
        var result = await model.OnPostExportSanitizedDbAsync();

        // Should return a file
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/octet-stream", fileResult.ContentType);
        Assert.StartsWith("hpoll-sanitized-", fileResult.FileDownloadName);
        Assert.EndsWith(".db", fileResult.FileDownloadName);

        // Open the exported DB and verify sanitization
        var exportPath = Path.Combine(Path.GetTempPath(), $"hpoll-verify-{Guid.NewGuid()}.db");
        try
        {
            await File.WriteAllBytesAsync(exportPath, fileResult.FileContents);
            using var conn = new SqliteConnection($"Data Source={exportPath}");
            await conn.OpenAsync();

            // Verify hub tokens are cleared
            using var hubCmd = conn.CreateCommand();
            hubCmd.CommandText = "SELECT AccessToken, RefreshToken, HueApplicationKey, TokenExpiresAt FROM Hubs";
            using var hubReader = await hubCmd.ExecuteReaderAsync();
            Assert.True(await hubReader.ReadAsync());
            Assert.Equal("", hubReader.GetString(0)); // AccessToken
            Assert.Equal("", hubReader.GetString(1)); // RefreshToken
            Assert.Equal("", hubReader.GetString(2)); // HueApplicationKey

            // Verify customer emails are cleared
            using var custCmd = conn.CreateCommand();
            custCmd.CommandText = "SELECT Name, Email, CcEmails, BccEmails FROM Customers";
            using var custReader = await custCmd.ExecuteReaderAsync();
            Assert.True(await custReader.ReadAsync());
            Assert.Equal("Test Customer", custReader.GetString(0)); // Name preserved
            Assert.Equal("", custReader.GetString(1)); // Email cleared
            Assert.Equal("", custReader.GetString(2)); // CcEmails cleared
            Assert.Equal("", custReader.GetString(3)); // BccEmails cleared
        }
        finally
        {
            try { File.Delete(exportPath); } catch { }
        }
    }

    [Fact]
    public async Task ExportSanitizedDb_PreservesNonSensitiveData()
    {
        var customer = new Customer { Name = "Keep This", Email = "remove@example.com", TimeZoneId = "Australia/Sydney" };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "BRIDGE123",
            HueApplicationKey = "secret",
            AccessToken = "secret",
            RefreshToken = "secret",
            TokenExpiresAt = DateTime.UtcNow,
            Status = HubStatus.Active
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        var device = new Device { HubId = hub.Id, HueDeviceId = "dev-001", DeviceType = DeviceTypes.MotionSensor, Name = "Living Room" };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        var model = CreateModel();
        var result = await model.OnPostExportSanitizedDbAsync();

        var fileResult = Assert.IsType<FileContentResult>(result);
        var exportPath = Path.Combine(Path.GetTempPath(), $"hpoll-verify-{Guid.NewGuid()}.db");
        try
        {
            await File.WriteAllBytesAsync(exportPath, fileResult.FileContents);
            using var conn = new SqliteConnection($"Data Source={exportPath}");
            await conn.OpenAsync();

            // Non-sensitive hub data preserved
            using var hubCmd = conn.CreateCommand();
            hubCmd.CommandText = "SELECT HueBridgeId, Status FROM Hubs";
            using var hubReader = await hubCmd.ExecuteReaderAsync();
            Assert.True(await hubReader.ReadAsync());
            Assert.Equal("BRIDGE123", hubReader.GetString(0));
            Assert.Equal(HubStatus.Active, hubReader.GetString(1));

            // Device data preserved
            using var devCmd = conn.CreateCommand();
            devCmd.CommandText = "SELECT Name, HueDeviceId FROM Devices";
            using var devReader = await devCmd.ExecuteReaderAsync();
            Assert.True(await devReader.ReadAsync());
            Assert.Equal("Living Room", devReader.GetString(0));
            Assert.Equal("dev-001", devReader.GetString(1));
        }
        finally
        {
            try { File.Delete(exportPath); } catch { }
        }
    }
}

/// <summary>Minimal ITempDataProvider for unit tests (stores in memory, no session needed).</summary>
internal class TestTempDataProvider : ITempDataProvider
{
    private IDictionary<string, object?> _data = new Dictionary<string, object?>();
    public IDictionary<string, object?> LoadTempData(HttpContext context) => _data;
    public void SaveTempData(HttpContext context, IDictionary<string, object?> values) => _data = values;
}
