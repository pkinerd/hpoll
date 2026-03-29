using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Hpoll.Core;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Pages;

public class AboutModel : PageModel
{
    private static readonly SemaphoreSlim _exportLock = new(1, 1);

    private readonly HpollDbContext _db;
    private readonly HueAppSettings _hueApp;
    private readonly ILogger<AboutModel> _logger;

    public AboutModel(HpollDbContext db, IOptions<HueAppSettings> hueApp, ILogger<AboutModel> logger)
    {
        _db = db;
        _hueApp = hueApp.Value;
        _logger = logger;
    }

    public int CustomerCount { get; set; }
    public int HubCount { get; set; }
    public int DeviceCount { get; set; }

    public List<BuildInfoEntry> BuildEntries { get; set; } = new();
    public List<(string Category, List<SystemInfoEntry> Entries)> Sections { get; set; } = new();

    public async Task OnGetAsync()
    {
        CustomerCount = await _db.Customers.CountAsync();
        HubCount = await _db.Hubs.CountAsync();
        DeviceCount = await _db.Devices.CountAsync();

        // Build info (baked into assembly at compile time)
        PopulateBuildInfo();
        BuildEntries.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.Ordinal));

        var entries = await _db.SystemInfo
            .OrderBy(e => e.Category)
            .ThenBy(e => e.Key)
            .AsNoTracking()
            .ToListAsync();

        var grouped = entries
            .GroupBy(e => e.Category)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => new SystemInfoEntry
                {
                    Label = FormatLabel(e.Key),
                    Value = FormatValue(e.Value),
                    UpdatedAt = e.UpdatedAt
                }).ToList());

        // Explicit section ordering (rename "Build" to "Worker Build" for clarity)
        var categoryOrder = new[] { "Build", "System", "Polling", "Email", "Hue", "Backup", "Runtime" };
        var categoryDisplayNames = new Dictionary<string, string> { ["Build"] = "Worker Build" };
        foreach (var cat in categoryOrder)
        {
            if (grouped.TryGetValue(cat, out var list))
            {
                var displayName = categoryDisplayNames.GetValueOrDefault(cat, cat);
                Sections.Add((displayName, list));
            }
        }

        // Any categories not in the explicit order
        foreach (var kvp in grouped)
        {
            if (!categoryOrder.Contains(kvp.Key))
                Sections.Add((kvp.Key, kvp.Value));
        }

        // Always surface the Admin's own Hue callback URL from config
        EnsureAdminCallbackUrl();

    }

    public async Task<IActionResult> OnPostExportSanitizedDbAsync()
    {
        if (!await _exportLock.WaitAsync(TimeSpan.Zero))
        {
            TempData["Error"] = "An export is already in progress. Please try again shortly.";
            return RedirectToPage();
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"hpoll-export-{Guid.NewGuid()}.db");
        try
        {
            _logger.LogInformation("Starting sanitized database export");

            // Create a consistent snapshot using VACUUM INTO (same pattern as DatabaseBackupService)
#pragma warning disable EF1002
            await _db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{tempPath}'");
#pragma warning restore EF1002

            // Open the copy directly and sanitize sensitive fields
            using (var conn = new SqliteConnection($"Data Source={tempPath}"))
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    UPDATE Hubs SET AccessToken = '', RefreshToken = '', HueApplicationKey = '', TokenExpiresAt = '0001-01-01T00:00:00';
                    UPDATE Customers SET Email = '', CcEmails = '', BccEmails = '';
                    VACUUM;
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(tempPath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

            _logger.LogInformation("Sanitized database export completed ({SizeKB:F1} KB)", bytes.Length / 1024.0);

            return File(bytes, "application/octet-stream", $"hpoll-sanitized-{timestamp}.db");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export sanitized database");
            TempData["Error"] = "Failed to export database. Check logs for details.";
            return RedirectToPage();
        }
        finally
        {
            try { System.IO.File.Delete(tempPath); } catch { /* best effort cleanup */ }
            _exportLock.Release();
        }
    }

    private void EnsureAdminCallbackUrl()
    {
        var callbackUrl = _hueApp.CallbackUrl;
        if (string.IsNullOrEmpty(callbackUrl))
            return;

        var hueIndex = Sections.FindIndex(s => s.Category == "Hue");
        if (hueIndex >= 0)
        {
            var hueEntries = Sections[hueIndex].Entries;
            // Only add if the Worker hasn't already written a non-empty callback_url to SystemInfo
            if (!hueEntries.Any(e => e.Label == "Callback Url" && !string.IsNullOrEmpty(e.Value)))
            {
                hueEntries.Add(new SystemInfoEntry { Label = "Callback Url", Value = callbackUrl });
            }
        }
        else
        {
            // Worker hasn't started yet — create a Hue section from Admin config
            Sections.Add(("Hue", new List<SystemInfoEntry>
            {
                new() { Label = "App Configured", Value = (!string.IsNullOrEmpty(_hueApp.ClientId)).ToString() },
                new() { Label = "Callback Url", Value = callbackUrl },
            }));
        }
    }

    private void PopulateBuildInfo()
    {
        void Add(string label, string value)
        {
            if (!string.IsNullOrEmpty(value))
                BuildEntries.Add(new BuildInfoEntry { Label = label, Value = value });
        }

        Add("Branch", BuildInfo.Branch);
        Add("Commit", BuildInfo.ShortCommit);
        Add("Build Number", BuildInfo.BuildNumber);
        Add("Run ID", BuildInfo.RunId);
        if (!string.IsNullOrEmpty(BuildInfo.PullRequest))
            Add("Pull Request", $"#{BuildInfo.PullRequest}");
        Add("Timestamp", BuildInfo.Timestamp);
        Add("Source", BuildInfo.IsCI ? "CI" : "Local");
    }

    private static readonly Dictionary<string, string> LabelOverrides = new()
    {
        ["Run Id"] = "Run ID",
    };

    private static string FormatLabel(string key)
    {
        var label = key;
        var dotIndex = label.IndexOf('.');
        if (dotIndex >= 0)
            label = label[(dotIndex + 1)..];
        var formatted = string.Join(' ', label.Split('_')
            .Select(w => w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));
        return LabelOverrides.GetValueOrDefault(formatted, formatted);
    }

    private static string FormatValue(string value)
    {
        if (DateTime.TryParse(value, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
        }
        return value;
    }
}

public class BuildInfoEntry
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class SystemInfoEntry
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
