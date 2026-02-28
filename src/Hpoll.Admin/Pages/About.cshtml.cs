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
    private readonly HueAppSettings _hueApp;
    private readonly IConfiguration _config;

    public AboutModel(
        HpollDbContext db,
        IOptions<HueAppSettings> hueApp,
        IConfiguration config)
    {
        _db = db;
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

        // Hue
        HueAppConfigured = !string.IsNullOrEmpty(_hueApp.ClientId);
        HueCallbackUrl = _hueApp.CallbackUrl;
    }
}
