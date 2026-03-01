using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Hpoll.Core.Interfaces;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Pages.Hubs;

[AllowAnonymous]
public class OAuthCallbackModel : PageModel
{
    private readonly HpollDbContext _db;
    private readonly IHueApiClient _hueClient;
    private readonly ILogger<OAuthCallbackModel> _logger;

    public OAuthCallbackModel(HpollDbContext db, IHueApiClient hueClient, ILogger<OAuthCallbackModel> logger)
    {
        _db = db;
        _hueClient = hueClient;
        _logger = logger;
    }

    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? BridgeId { get; set; }
    public int? DeviceCount { get; set; }
    public int? CustomerId { get; set; }
    public int? HubId { get; set; }

    public async Task<IActionResult> OnGetAsync(string? code, string? state, string? error)
    {
        // Handle OAuth error response
        if (!string.IsNullOrEmpty(error))
        {
            Message = $"Hue authorization was denied: {error}";
            return Page();
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            Message = "Missing authorization code or state parameter.";
            return Page();
        }

        // Validate CSRF token from state
        var parts = state.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var customerId))
        {
            Message = "Invalid state parameter format.";
            return Page();
        }

        var expectedCsrf = HttpContext.Session.GetString("OAuthCsrf");
        var expectedCustomerId = HttpContext.Session.GetInt32("OAuthCustomerId");

        if (expectedCsrf == null || parts[1] != expectedCsrf || expectedCustomerId != customerId)
        {
            Message = "CSRF validation failed. Please try the registration again from the customer page.";
            return Page();
        }

        // Clear session tokens
        HttpContext.Session.Remove("OAuthCsrf");
        HttpContext.Session.Remove("OAuthCustomerId");

        CustomerId = customerId;
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer == null)
        {
            Message = "Customer not found.";
            return Page();
        }

        try
        {
            // Step 1: Exchange auth code for tokens
            var callbackUrl = HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + "/Hubs/OAuthCallback";
            _logger.LogInformation("Exchanging auth code for customer {CustomerId}", customerId);
            var tokenResponse = await _hueClient.ExchangeAuthorizationCodeAsync(code, callbackUrl);

            // Step 2: Enable link button
            _logger.LogInformation("Enabling link button for customer {CustomerId}", customerId);
            await _hueClient.EnableLinkButtonAsync(tokenResponse.AccessToken);

            // Step 3: Register application to get hue_application_key
            _logger.LogInformation("Registering application for customer {CustomerId}", customerId);
            var applicationKey = await _hueClient.RegisterApplicationAsync(tokenResponse.AccessToken);

            // Step 4: Get bridge ID
            _logger.LogInformation("Getting bridge ID for customer {CustomerId}", customerId);
            var bridgeId = await _hueClient.GetBridgeIdAsync(tokenResponse.AccessToken, applicationKey);
            BridgeId = bridgeId;

            // Check if hub with this bridge ID already exists
            var existingHub = await _db.Hubs.FirstOrDefaultAsync(h => h.HueBridgeId == bridgeId);
            if (existingHub != null)
            {
                // Update existing hub's tokens
                existingHub.AccessToken = tokenResponse.AccessToken;
                existingHub.RefreshToken = tokenResponse.RefreshToken;
                existingHub.TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                existingHub.HueApplicationKey = applicationKey;
                existingHub.Status = "active";
                existingHub.ConsecutiveFailures = 0;
                existingHub.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                HubId = existingHub.Id;
                Message = $"Hub {bridgeId} already existed — tokens updated and status set to active.";
                Success = true;
            }
            else
            {
                // Create new hub
                var hub = new Hub
                {
                    CustomerId = customerId,
                    HueBridgeId = bridgeId,
                    HueApplicationKey = applicationKey,
                    AccessToken = tokenResponse.AccessToken,
                    RefreshToken = tokenResponse.RefreshToken,
                    TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                    Status = "active"
                };
                _db.Hubs.Add(hub);
                await _db.SaveChangesAsync();
                HubId = hub.Id;

                // Step 5: Test connectivity — get device count
                try
                {
                    var devices = await _hueClient.GetDevicesAsync(tokenResponse.AccessToken, applicationKey);
                    DeviceCount = devices.Data.Count;
                }
                catch (Exception ex)
                {
                    // Non-fatal — hub is registered, devices will be discovered during polling
                    _logger.LogWarning(ex, "Failed to get device count for newly registered hub {BridgeId}", bridgeId);
                }

                Message = $"Hub {bridgeId} registered successfully.";
                Success = true;
            }

            _logger.LogInformation("Hub registration complete for bridge {BridgeId}, customer {CustomerId}", bridgeId, customerId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "OAuth hub registration failed for customer {CustomerId}", customerId);
            Message = ex.StatusCode.HasValue
                ? $"Hub registration failed: Hue API returned HTTP {(int)ex.StatusCode}."
                : "Hub registration failed: could not reach the Hue API.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth hub registration failed for customer {CustomerId}", customerId);
            Message = "Hub registration failed due to an unexpected error. Check the server logs for details.";
        }

        return Page();
    }
}
