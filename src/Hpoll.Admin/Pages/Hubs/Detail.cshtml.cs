using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Hpoll.Core.Interfaces;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Pages.Hubs;

public class DetailModel : PageModel
{
    private readonly HpollDbContext _db;
    private readonly IHueApiClient _hueClient;
    private readonly ILogger<DetailModel> _logger;

    public DetailModel(HpollDbContext db, IHueApiClient hueClient, ILogger<DetailModel> logger)
    {
        _db = db;
        _hueClient = hueClient;
        _logger = logger;
    }

    public Hub Hub { get; set; } = null!;
    public List<Device> Devices { get; set; } = new();
    public List<PollingLog> RecentLogs { get; set; } = new();
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        return await LoadHub(id);
    }

    public async Task<IActionResult> OnPostToggleStatusAsync(int id)
    {
        var hub = await _db.Hubs.FindAsync(id);
        if (hub == null) return NotFound();

        hub.Status = hub.Status == "active" ? "inactive" : "active";
        hub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostClearReauthAsync(int id)
    {
        var hub = await _db.Hubs.FindAsync(id);
        if (hub == null) return NotFound();

        if (hub.Status == "needs_reauth")
        {
            hub.Status = "active";
            hub.ConsecutiveFailures = 0;
            hub.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRefreshTokenAsync(int id)
    {
        var hub = await _db.Hubs.FindAsync(id);
        if (hub == null) return NotFound();

        try
        {
            var tokenResponse = await _hueClient.RefreshTokenAsync(hub.RefreshToken);

            hub.AccessToken = tokenResponse.AccessToken;
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                hub.RefreshToken = tokenResponse.RefreshToken;
            hub.TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            hub.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Manual token refresh for hub {BridgeId}: new expiry {Expiry}",
                hub.HueBridgeId, hub.TokenExpiresAt);
            SuccessMessage = $"Token refreshed. New expiry: {hub.TokenExpiresAt:yyyy-MM-dd HH:mm} UTC";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Manual token refresh failed for hub {BridgeId}", hub.HueBridgeId);
            ErrorMessage = $"Token refresh failed: {ex.Message}";
        }

        return await LoadHub(id);
    }

    private async Task<IActionResult> LoadHub(int id)
    {
        var hub = await _db.Hubs
            .Include(h => h.Customer)
            .FirstOrDefaultAsync(h => h.Id == id);

        if (hub == null) return NotFound();
        Hub = hub;

        Devices = await _db.Devices
            .Where(d => d.HubId == id)
            .OrderBy(d => d.Name)
            .ToListAsync();

        RecentLogs = await _db.PollingLogs
            .Where(l => l.HubId == id)
            .OrderByDescending(l => l.Timestamp)
            .Take(20)
            .ToListAsync();

        return Page();
    }
}
