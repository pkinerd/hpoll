using System.Reflection;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Data;

namespace Hpoll.Admin.Pages;

public class AboutModel : PageModel
{
    private readonly HpollDbContext _db;
    private readonly PollingSettings _polling;
    private readonly EmailSettings _email;
    private readonly HueAppSettings _hueApp;
    private readonly IConfiguration _config;

    public AboutModel(
        HpollDbContext db,
        IOptions<PollingSettings> polling,
        IOptions<EmailSettings> email,
        IOptions<HueAppSettings> hueApp,
        IConfiguration config)
    {
        _db = db;
        _polling = polling.Value;
        _email = email.Value;
        _hueApp = hueApp.Value;
        _config = config;
    }

    public string Version { get; set; } = string.Empty;
    public string Runtime { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string DatabasePath { get; set; } = string.Empty;
    public int CustomerCount { get; set; }
    public int HubCount { get; set; }
    public int DeviceCount { get; set; }

    // Polling config
    public int PollingIntervalMinutes { get; set; }
    public int BatteryPollIntervalHours { get; set; }
    public int DataRetentionHours { get; set; }
    public int HttpTimeoutSeconds { get; set; }
    public int TokenRefreshCheckHours { get; set; }
    public int TokenRefreshThresholdHours { get; set; }
    public int HealthFailureThreshold { get; set; }
    public int HealthMaxSilenceHours { get; set; }

    // Email config
    public List<string> EmailSendTimes { get; set; } = new();
    public int BatteryAlertThreshold { get; set; }
    public int SummaryWindowHours { get; set; }
    public int SummaryWindowCount { get; set; }

    // Hue config (non-sensitive)
    public bool HueAppConfigured { get; set; }
    public string? HueCallbackUrl { get; set; }

    public async Task OnGetAsync()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        Version = informationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";

        Runtime = $".NET {System.Environment.Version}";
        Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        DatabasePath = _config.GetValue<string>("DataPath") ?? "data";

        CustomerCount = await _db.Customers.CountAsync();
        HubCount = await _db.Hubs.CountAsync();
        DeviceCount = await _db.Devices.CountAsync();

        // Polling
        PollingIntervalMinutes = _polling.IntervalMinutes;
        BatteryPollIntervalHours = _polling.BatteryPollIntervalHours;
        DataRetentionHours = _polling.DataRetentionHours;
        HttpTimeoutSeconds = _polling.HttpTimeoutSeconds;
        TokenRefreshCheckHours = _polling.TokenRefreshCheckHours;
        TokenRefreshThresholdHours = _polling.TokenRefreshThresholdHours;
        HealthFailureThreshold = _polling.HealthFailureThreshold;
        HealthMaxSilenceHours = _polling.HealthMaxSilenceHours;

        // Email
        EmailSendTimes = _email.SendTimesUtc;
        BatteryAlertThreshold = _email.BatteryAlertThreshold;
        SummaryWindowHours = _email.SummaryWindowHours;
        SummaryWindowCount = _email.SummaryWindowCount;

        // Hue
        HueAppConfigured = !string.IsNullOrEmpty(_hueApp.ClientId);
        HueCallbackUrl = _hueApp.CallbackUrl;
    }
}
