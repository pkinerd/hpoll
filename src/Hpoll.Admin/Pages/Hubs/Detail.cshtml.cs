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

        if (hub.Status == "active")
        {
            hub.Status = "inactive";
            hub.DeactivatedAt = DateTime.UtcNow;
        }
        else
        {
            hub.Status = "active";
            hub.DeactivatedAt = null;
        }
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
            hub.DeactivatedAt = null;
            hub.ConsecutiveFailures = 0;
            hub.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var hub = await _db.Hubs.FindAsync(id);
        if (hub == null) return NotFound();

        if (hub.Status != "inactive" || hub.DeactivatedAt == null ||
            (DateTime.UtcNow - hub.DeactivatedAt.Value).TotalHours < 24)
        {
            return RedirectToPage(new { id });
        }

        var customerId = hub.CustomerId;
        _db.Hubs.Remove(hub);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Customers/Detail", new { id = customerId });
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
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Manual token refresh failed for hub {BridgeId}", hub.HueBridgeId);
            ErrorMessage = ex.StatusCode.HasValue
                ? $"Token refresh failed: Hue API returned HTTP {(int)ex.StatusCode}."
                : "Token refresh failed: could not reach the Hue API.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Manual token refresh failed for hub {BridgeId}", hub.HueBridgeId);
            ErrorMessage = "Token refresh failed due to an unexpected error. Check the server logs for details.";
        }

        return await LoadHub(id);
    }

    public async Task<IActionResult> OnPostTestConnectionAsync(int id)
    {
        var hub = await _db.Hubs.FindAsync(id);
        if (hub == null) return NotFound();

        try
        {
            var devices = await _hueClient.GetDevicesAsync(hub.AccessToken, hub.HueApplicationKey);
            SuccessMessage = $"Connection successful. Found {devices.Data.Count} device(s) on this bridge.";
            _logger.LogInformation("API connection test succeeded for hub {BridgeId}: {Count} devices",
                hub.HueBridgeId, devices.Data.Count);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "API connection test failed for hub {BridgeId}", hub.HueBridgeId);
            ErrorMessage = ex.StatusCode.HasValue
                ? $"Connection test failed: Hue API returned HTTP {(int)ex.StatusCode}."
                : "Connection test failed: could not reach the Hue API.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "API connection test failed for hub {BridgeId}", hub.HueBridgeId);
            ErrorMessage = "Connection test failed due to an unexpected error. Check the server logs for details.";
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
