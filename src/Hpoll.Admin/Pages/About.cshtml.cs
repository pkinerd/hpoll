using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Hpoll.Core;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Pages;

public class AboutModel : PageModel
{
    private readonly HpollDbContext _db;

    public AboutModel(HpollDbContext db)
    {
        _db = db;
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

        var entries = await _db.SystemInfo
            .OrderBy(e => e.Category)
            .ThenBy(e => e.Key)
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

        // Explicit section ordering
        var categoryOrder = new[] { "Build", "System", "Polling", "Email", "Hue", "Runtime" };
        foreach (var cat in categoryOrder)
        {
            if (grouped.TryGetValue(cat, out var list))
                Sections.Add((cat, list));
        }

        // Any categories not in the explicit order
        foreach (var kvp in grouped)
        {
            if (!categoryOrder.Contains(kvp.Key))
                Sections.Add((kvp.Key, kvp.Value));
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
        Add("Built At", BuildInfo.Timestamp);
        Add("Source", BuildInfo.IsCI ? "CI" : "Local");
    }

    private static string FormatLabel(string key)
    {
        var label = key;
        var dotIndex = label.IndexOf('.');
        if (dotIndex >= 0)
            label = label[(dotIndex + 1)..];
        return string.Join(' ', label.Split('_')
            .Select(w => w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));
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
