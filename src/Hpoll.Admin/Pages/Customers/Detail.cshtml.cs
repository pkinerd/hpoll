using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Pages.Customers;

public class DetailModel : PageModel
{
    private readonly HpollDbContext _db;
    private readonly HueAppSettings _hueApp;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<DetailModel> _logger;

    public DetailModel(HpollDbContext db, IOptions<HueAppSettings> hueApp, IOptions<EmailSettings> emailSettings, ILogger<DetailModel> logger)
    {
        _db = db;
        _hueApp = hueApp.Value;
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public Customer Customer { get; set; } = null!;

    [BindProperty, EmailAddress]
    public string? EditEmail { get; set; }

    [BindProperty, StringLength(100)]
    public string? EditName { get; set; }

    [BindProperty]
    public string? EditCcEmails { get; set; }

    [BindProperty]
    public string? EditBccEmails { get; set; }

    public string? SuccessMessage { get; set; }
    public string? OAuthUrl { get; set; }
    public bool ShowActivitySummary { get; set; }
    public List<ActivityWindow> ActivityWindows { get; set; } = new();
    public int MotionSensorCount { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, bool? activity = null)
    {
        var customer = await _db.Customers
            .Include(c => c.Hubs)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer == null) return NotFound();
        Customer = customer;
        EditEmail = customer.Email;
        EditName = customer.Name;
        EditCcEmails = customer.CcEmails;
        EditBccEmails = customer.BccEmails;

        if (activity == true)
        {
            ShowActivitySummary = true;
            await LoadActivitySummaryAsync(customer);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateNameAsync(int id)
    {
        var customer = await _db.Customers.Include(c => c.Hubs).FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();
        Customer = customer;
        EditEmail = customer.Email;
        EditCcEmails = customer.CcEmails;
        EditBccEmails = customer.BccEmails;

        if (string.IsNullOrWhiteSpace(EditName))
        {
            ModelState.AddModelError(nameof(EditName), "Name is required.");
            return Page();
        }

        customer.Name = EditName!.Trim();
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        SuccessMessage = "Name updated.";
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateEmailAsync(int id)
    {
        var customer = await _db.Customers.Include(c => c.Hubs).FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();
        Customer = customer;
        EditName = customer.Name;
        EditCcEmails = customer.CcEmails;
        EditBccEmails = customer.BccEmails;

        if (!ModelState.IsValid) return Page();

        customer.Email = EditEmail!;
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        SuccessMessage = "Email updated.";
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateCcBccAsync(int id)
    {
        var customer = await _db.Customers.Include(c => c.Hubs).FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();
        Customer = customer;
        EditEmail = customer.Email;
        EditName = customer.Name;

        customer.CcEmails = (EditCcEmails ?? string.Empty).Trim();
        customer.BccEmails = (EditBccEmails ?? string.Empty).Trim();
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        SuccessMessage = "CC/BCC lists updated.";
        EditCcEmails = customer.CcEmails;
        EditBccEmails = customer.BccEmails;
        return Page();
    }

    public async Task<IActionResult> OnPostToggleStatusAsync(int id)
    {
        var customer = await _db.Customers.Include(c => c.Hubs).FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();

        customer.Status = customer.Status == "active" ? "inactive" : "active";
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRegisterHubAsync(int id)
    {
        var customer = await _db.Customers.Include(c => c.Hubs).FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();
        Customer = customer;
        EditEmail = customer.Email;
        EditName = customer.Name;
        EditCcEmails = customer.CcEmails;
        EditBccEmails = customer.BccEmails;

        if (string.IsNullOrEmpty(_hueApp.ClientId) || string.IsNullOrEmpty(_hueApp.CallbackUrl))
        {
            SuccessMessage = "HueApp:ClientId and HueApp:CallbackUrl must be configured.";
            return Page();
        }

        // Generate CSRF token and store in session
        var csrfToken = Guid.NewGuid().ToString("N");
        HttpContext.Session.SetString("OAuthCsrf", csrfToken);
        HttpContext.Session.SetInt32("OAuthCustomerId", id);

        var state = $"{id}:{csrfToken}";
        OAuthUrl = $"https://api.meethue.com/v2/oauth2/authorize" +
            $"?client_id={Uri.EscapeDataString(_hueApp.ClientId)}" +
            $"&response_type=code" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&redirect_uri={Uri.EscapeDataString(_hueApp.CallbackUrl)}";

        return Page();
    }

    private async Task LoadActivitySummaryAsync(Customer customer)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(customer.TimeZoneId);
        var nowUtc = DateTime.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

        var windowHours = _emailSettings.SummaryWindowHours;
        var windowCount = _emailSettings.SummaryWindowCount;
        var totalHours = windowCount * windowHours;

        var startUtc = nowUtc.AddHours(-(totalHours + windowHours));

        var bucketEndLocal = nowLocal.Date.AddHours(nowLocal.Hour / windowHours * windowHours + windowHours);
        var bucketStartLocal = bucketEndLocal.AddHours(-totalHours);

        var hubIds = await _db.Hubs
            .Where(h => h.CustomerId == customer.Id && h.Status == "active")
            .Select(h => h.Id)
            .ToListAsync();

        var deviceIds = await _db.Devices
            .Where(d => hubIds.Contains(d.HubId))
            .Select(d => d.Id)
            .ToListAsync();

        MotionSensorCount = await _db.Devices
            .Where(d => hubIds.Contains(d.HubId) && d.DeviceType == "motion_sensor")
            .CountAsync();

        var readings = await _db.DeviceReadings
            .Where(r => deviceIds.Contains(r.DeviceId)
                && r.Timestamp >= startUtc && r.Timestamp < nowUtc
                && (r.ReadingType == "motion" || r.ReadingType == "temperature"))
            .AsNoTracking()
            .ToListAsync();

        for (int i = 0; i < windowCount; i++)
        {
            var windowStartLocal = bucketStartLocal.AddHours(i * windowHours);
            var windowEndLocal = windowStartLocal.AddHours(windowHours);
            var windowStartUtc = TimeZoneInfo.ConvertTimeToUtc(windowStartLocal, tz);
            var windowEndUtc = TimeZoneInfo.ConvertTimeToUtc(windowEndLocal, tz);

            var windowReadings = readings.Where(r => r.Timestamp >= windowStartUtc && r.Timestamp < windowEndUtc).ToList();
            var motionReadings = windowReadings.Where(r => r.ReadingType == "motion").ToList();
            var tempReadings = windowReadings.Where(r => r.ReadingType == "temperature").ToList();

            var devicesWithMotion = motionReadings
                .Where(r => {
                    try { using var j = JsonDocument.Parse(r.Value); return j.RootElement.GetProperty("motion").GetBoolean(); }
                    catch (JsonException ex) { _logger.LogWarning(ex, "Failed to parse motion reading value for DeviceId {DeviceId}", r.DeviceId); return false; }
                })
                .Select(r => r.DeviceId)
                .Distinct()
                .Count();

            var totalMotionEvents = motionReadings
                .Count(r => {
                    try { using var j = JsonDocument.Parse(r.Value); return j.RootElement.GetProperty("motion").GetBoolean(); }
                    catch (JsonException ex) { _logger.LogWarning(ex, "Failed to parse motion reading value for DeviceId {DeviceId}", r.DeviceId); return false; }
                });

            var temperatures = tempReadings
                .Select(r => {
                    try { using var j = JsonDocument.Parse(r.Value); return (double?)j.RootElement.GetProperty("temperature").GetDouble(); }
                    catch (JsonException ex) { _logger.LogWarning(ex, "Failed to parse temperature reading value for DeviceId {DeviceId}", r.DeviceId); return null; }
                })
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .OrderBy(t => t)
                .ToList();

            var displayEnd = windowEndLocal > nowLocal ? nowLocal : windowEndLocal;
            ActivityWindows.Add(new ActivityWindow
            {
                Label = $"{windowStartLocal:HH:mm}\u2013{displayEnd:HH:mm}",
                DevicesWithMotion = devicesWithMotion,
                TotalMotionSensors = MotionSensorCount > 0 ? MotionSensorCount : 1,
                TotalMotionEvents = totalMotionEvents,
                TemperatureMin = temperatures.Count > 0 ? temperatures.First() : null,
                TemperatureMedian = temperatures.Count > 0 ? temperatures[temperatures.Count / 2] : null,
                TemperatureMax = temperatures.Count > 0 ? temperatures.Last() : null,
            });
        }

        ActivityWindows.Reverse();
    }

    public class ActivityWindow
    {
        public string Label { get; set; } = string.Empty;
        public int DevicesWithMotion { get; set; }
        public int TotalMotionSensors { get; set; }
        public int TotalMotionEvents { get; set; }
        public double? TemperatureMin { get; set; }
        public double? TemperatureMedian { get; set; }
        public double? TemperatureMax { get; set; }
    }
}
