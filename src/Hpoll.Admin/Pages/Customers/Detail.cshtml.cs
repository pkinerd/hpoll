using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hpoll.Admin.Services;
using Hpoll.Core.Configuration;
using Hpoll.Core.Constants;
using Hpoll.Core.Interfaces;
using Hpoll.Core.Services;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Pages.Customers;

public class DetailModel : PageModel
{
    private readonly HpollDbContext _db;
    private readonly HueAppSettings _hueApp;
    private readonly EmailSettings _emailSettings;
    private readonly SendTimeDisplayService _sendTimeService;
    private readonly IEmailRenderer _emailRenderer;
    private readonly ILogger<DetailModel> _logger;

    public DetailModel(HpollDbContext db, IOptions<HueAppSettings> hueApp, IOptions<EmailSettings> emailSettings, SendTimeDisplayService sendTimeService, IEmailRenderer emailRenderer, ILogger<DetailModel> logger)
    {
        _db = db;
        _hueApp = hueApp.Value;
        _emailSettings = emailSettings.Value;
        _sendTimeService = sendTimeService;
        _emailRenderer = emailRenderer;
        _logger = logger;
    }

    public Customer Customer { get; set; } = null!;

    [BindProperty]
    public string? EditEmail { get; set; }

    [BindProperty, StringLength(100)]
    public string? EditName { get; set; }

    [BindProperty]
    public string? EditCcEmails { get; set; }

    [BindProperty]
    public string? EditBccEmails { get; set; }

    [BindProperty]
    public string? EditSendTimesLocal { get; set; }

    [BindProperty]
    public string? EditTimeZoneId { get; set; }

    [BindProperty]
    public int? EditSummaryWindowOffsetHours { get; set; }

    [BindProperty]
    public int? EditSummaryWindowHours { get; set; }

    [BindProperty]
    public int? EditSummaryWindowCount { get; set; }

    [BindProperty]
    public bool EditIncludeLatestLocations { get; set; }

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public string DefaultSendTimesDisplay { get; set; } = string.Empty;
    public int DefaultWindowHours { get; set; }
    public int DefaultWindowCount { get; set; }
    public int DefaultWindowOffset { get; set; }
    public string? OAuthUrl { get; set; }
    public List<ActivityWindow> ActivityWindows { get; set; } = new();
    public int MotionSensorCount { get; set; }
    public List<BatteryStatus> BatteryStatuses { get; set; } = new();
    public List<UnreachableDevice> UnreachableDevices { get; set; } = new();
    public string EmailPreviewHtml { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int id)
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
        EditSendTimesLocal = customer.SendTimesLocal;
        EditTimeZoneId = customer.TimeZoneId;
        EditSummaryWindowOffsetHours = customer.SummaryWindowOffsetHours;
        EditSummaryWindowHours = customer.SummaryWindowHours;
        EditSummaryWindowCount = customer.SummaryWindowCount;
        EditIncludeLatestLocations = customer.IncludeLatestLocations;
        DefaultSendTimesDisplay = await _sendTimeService.GetDefaultSendTimesDisplayAsync();
        DefaultWindowHours = _emailSettings.SummaryWindowHours;
        DefaultWindowCount = _emailSettings.SummaryWindowCount;
        DefaultWindowOffset = _emailSettings.SummaryWindowOffsetHours;
        await LoadActivitySummaryAsync(customer);
        await LoadBatteryStatusAsync(customer);
        await LoadEmailPreviewAsync(customer);

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateSettingsAsync(int id)
    {
        var customer = await _db.Customers.Include(c => c.Hubs).FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();

        // Validate name
        if (string.IsNullOrWhiteSpace(EditName))
            ModelState.AddModelError(nameof(EditName), "Name is required.");

        // Validate emails
        if (string.IsNullOrWhiteSpace(EditEmail))
            ModelState.AddModelError(nameof(EditEmail), "At least one email address is required.");
        else
            ValidateEmailField(EditEmail, nameof(EditEmail));

        ValidateEmailField(EditCcEmails, nameof(EditCcEmails));
        ValidateEmailField(EditBccEmails, nameof(EditBccEmails));

        // Validate timezone
        var newTzId = (EditTimeZoneId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(newTzId))
        {
            ModelState.AddModelError(nameof(EditTimeZoneId), "Timezone is required.");
        }
        else
        {
            try { TimeZoneInfo.FindSystemTimeZoneById(newTzId); }
            catch (TimeZoneNotFoundException)
            {
                ModelState.AddModelError(nameof(EditTimeZoneId), "Invalid timezone.");
            }
        }

        // Validate send times
        var newSendTimes = (EditSendTimesLocal ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(newSendTimes))
        {
            var parsed = SendTimeHelper.ParseTimeSpans(newSendTimes);
            if (parsed.Count == 0)
            {
                ModelState.AddModelError(nameof(EditSendTimesLocal), "Invalid time format. Use HH:mm (e.g., 19:30, 08:00).");
            }
            else
            {
                parsed.Sort();
                newSendTimes = string.Join(", ", parsed.Select(t => $"{t:hh\\:mm}"));
            }
        }

        // Validate window settings
        if (EditSummaryWindowHours.HasValue && EditSummaryWindowHours.Value < 1)
            ModelState.AddModelError(nameof(EditSummaryWindowHours), "Window size must be at least 1 hour.");
        if (EditSummaryWindowCount.HasValue && EditSummaryWindowCount.Value < 1)
            ModelState.AddModelError(nameof(EditSummaryWindowCount), "Window count must be at least 1.");
        if (EditSummaryWindowOffsetHours.HasValue && EditSummaryWindowOffsetHours.Value < 0)
            ModelState.AddModelError(nameof(EditSummaryWindowOffsetHours), "Offset cannot be negative.");

        if (!ModelState.IsValid)
        {
            await PreparePageDataAsync(customer);
            return Page();
        }

        // Apply all changes
        customer.Name = EditName!.Trim();
        customer.Email = EditEmail!;
        customer.CcEmails = (EditCcEmails ?? string.Empty).Trim();
        customer.BccEmails = (EditBccEmails ?? string.Empty).Trim();
        customer.TimeZoneId = newTzId;
        customer.SendTimesLocal = newSendTimes;
        customer.SummaryWindowOffsetHours = EditSummaryWindowOffsetHours;
        customer.SummaryWindowHours = EditSummaryWindowHours;
        customer.SummaryWindowCount = EditSummaryWindowCount;
        customer.IncludeLatestLocations = EditIncludeLatestLocations;

        var effectiveDefaults = await _sendTimeService.GetEffectiveDefaultSendTimesUtcAsync();
        customer.NextSendTimeUtc = SendTimeHelper.ComputeNextSendTimeUtc(
            customer.SendTimesLocal, customer.TimeZoneId, DateTime.UtcNow, effectiveDefaults);
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        SuccessMessage = "Customer settings updated.";
        await PreparePageDataAsync(customer);
        return Page();
    }

    public async Task<IActionResult> OnPostToggleStatusAsync(int id)
    {
        var customer = await _db.Customers.Include(c => c.Hubs).FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();

        customer.Status = customer.Status == CustomerStatus.Active ? CustomerStatus.Inactive : CustomerStatus.Active;
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRegisterHubAsync(int id)
    {
        var customer = await _db.Customers.Include(c => c.Hubs).FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();

        if (string.IsNullOrEmpty(_hueApp.ClientId) || string.IsNullOrEmpty(_hueApp.CallbackUrl))
        {
            ErrorMessage = "HueApp:ClientId and HueApp:CallbackUrl must be configured.";
            await PreparePageDataAsync(customer);
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

        await PreparePageDataAsync(customer);
        return Page();
    }

    private void PopulateEditFields(Customer customer)
    {
        Customer = customer;
        EditName = customer.Name;
        EditEmail = customer.Email;
        EditCcEmails = customer.CcEmails;
        EditBccEmails = customer.BccEmails;
        EditSendTimesLocal = customer.SendTimesLocal;
        EditTimeZoneId = customer.TimeZoneId;
        EditSummaryWindowOffsetHours = customer.SummaryWindowOffsetHours;
        EditSummaryWindowHours = customer.SummaryWindowHours;
        EditSummaryWindowCount = customer.SummaryWindowCount;
        EditIncludeLatestLocations = customer.IncludeLatestLocations;
    }

    private async Task PreparePageDataAsync(Customer customer)
    {
        PopulateEditFields(customer);
        DefaultSendTimesDisplay = await _sendTimeService.GetDefaultSendTimesDisplayAsync();
        DefaultWindowHours = _emailSettings.SummaryWindowHours;
        DefaultWindowCount = _emailSettings.SummaryWindowCount;
        DefaultWindowOffset = _emailSettings.SummaryWindowOffsetHours;
        await LoadActivitySummaryAsync(customer);
        await LoadBatteryStatusAsync(customer);
        await LoadEmailPreviewAsync(customer);
    }

    private async Task LoadEmailPreviewAsync(Customer customer)
    {
        try
        {
            EmailPreviewHtml = await _emailRenderer.RenderDailySummaryAsync(customer.Id, customer.TimeZoneId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to render email preview for customer {CustomerId}", customer.Id);
            EmailPreviewHtml = string.Empty;
        }
    }

    private void ValidateEmailField(string? commaDelimited, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(commaDelimited)) return;
        var invalid = commaDelimited
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => !MailAddress.TryCreate(e, out _))
            .ToList();
        if (invalid.Count > 0)
            ModelState.AddModelError(fieldName, $"Invalid email address(es): {string.Join(", ", invalid)}");
    }

    private async Task LoadActivitySummaryAsync(Customer customer)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(customer.TimeZoneId);
        var nowUtc = DateTime.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

        var windowHours = customer.SummaryWindowHours ?? _emailSettings.SummaryWindowHours;
        var windowCount = customer.SummaryWindowCount ?? _emailSettings.SummaryWindowCount;
        var totalHours = windowCount * windowHours;

        var startUtc = nowUtc.AddHours(-(totalHours + windowHours));

        var offset = customer.SummaryWindowOffsetHours ?? _emailSettings.SummaryWindowOffsetHours;
        var adjHour = nowLocal.Hour - offset;
        var bucketEndLocal = nowLocal.Date.AddHours((int)Math.Floor((double)adjHour / windowHours) * windowHours + windowHours + offset);
        var bucketStartLocal = bucketEndLocal.AddHours(-totalHours);

        var hubIds = await _db.Hubs
            .Where(h => h.CustomerId == customer.Id && h.Status == HubStatus.Active)
            .Select(h => h.Id)
            .ToListAsync();

        var deviceIds = await _db.Devices
            .Where(d => hubIds.Contains(d.HubId))
            .Select(d => d.Id)
            .ToListAsync();

        var motionSensors = await _db.Devices
            .Where(d => hubIds.Contains(d.HubId) && d.DeviceType == DeviceTypes.MotionSensor)
            .AsNoTracking()
            .ToListAsync();

        MotionSensorCount = motionSensors.Count;
        var motionDeviceNames = motionSensors.ToDictionary(d => d.Id, d => d.Name);

        var readings = await _db.DeviceReadings
            .Where(r => deviceIds.Contains(r.DeviceId)
                && r.Timestamp >= startUtc && r.Timestamp < nowUtc
                && (r.ReadingType == ReadingTypes.Motion || r.ReadingType == ReadingTypes.Temperature))
            .AsNoTracking()
            .ToListAsync();

        for (int i = 0; i < windowCount; i++)
        {
            var windowStartLocal = bucketStartLocal.AddHours(i * windowHours);
            var windowEndLocal = windowStartLocal.AddHours(windowHours);
            var windowStartUtc = TimeZoneInfo.ConvertTimeToUtc(windowStartLocal, tz);
            var windowEndUtc = TimeZoneInfo.ConvertTimeToUtc(windowEndLocal, tz);

            var windowReadings = readings.Where(r => r.Timestamp >= windowStartUtc && r.Timestamp < windowEndUtc).ToList();
            var motionReadings = windowReadings.Where(r => r.ReadingType == ReadingTypes.Motion).ToList();
            var tempReadings = windowReadings.Where(r => r.ReadingType == ReadingTypes.Temperature).ToList();

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

            // Find the motion sensor with the latest "changed" timestamp in this window
            string? latestMotionSensor = null;
            DateTime latestChanged = DateTime.MinValue;
            foreach (var r in motionReadings)
            {
                try
                {
                    using var j = JsonDocument.Parse(r.Value);
                    if (!j.RootElement.GetProperty("motion").GetBoolean()) continue;
                    if (j.RootElement.TryGetProperty("changed", out var changedProp))
                    {
                        var changed = changedProp.GetDateTime();
                        if (changed > latestChanged)
                        {
                            latestChanged = changed;
                            latestMotionSensor = motionDeviceNames.TryGetValue(r.DeviceId, out var name) ? name : null;
                        }
                    }
                }
                catch (JsonException) { }
            }

            var displayEnd = windowEndLocal > nowLocal ? nowLocal : windowEndLocal;
            ActivityWindows.Add(new ActivityWindow
            {
                Label = $"{windowStartLocal:HH:mm}\u2013{displayEnd:HH:mm}",
                DevicesWithMotion = devicesWithMotion,
                TotalMotionSensors = MotionSensorCount > 0 ? MotionSensorCount : 1,
                TotalMotionEvents = totalMotionEvents,
                LatestMotionSensor = latestMotionSensor,
                TemperatureMin = temperatures.Count > 0 ? temperatures.First() : null,
                TemperatureMedian = temperatures.Count > 0 ? temperatures[temperatures.Count / 2] : null,
                TemperatureMax = temperatures.Count > 0 ? temperatures.Last() : null,
            });
        }

        ActivityWindows.Reverse();
    }

    private async Task LoadBatteryStatusAsync(Customer customer)
    {
        var hubIds = await _db.Hubs
            .Where(h => h.CustomerId == customer.Id && h.Status == HubStatus.Active)
            .Select(h => h.Id)
            .ToListAsync();

        var batteryDevices = await _db.Devices
            .Where(d => hubIds.Contains(d.HubId) && d.DeviceType == DeviceTypes.Battery)
            .ToListAsync();

        if (batteryDevices.Count > 0)
        {
            var batteryDeviceIds = batteryDevices.Select(d => d.Id).ToList();

            // Get the most recent battery reading for each device
            var latestReadings = await _db.DeviceReadings
                .Where(r => batteryDeviceIds.Contains(r.DeviceId) && r.ReadingType == ReadingTypes.Battery)
                .GroupBy(r => r.DeviceId)
                .Select(g => g.OrderByDescending(r => r.Timestamp).First())
                .AsNoTracking()
                .ToListAsync();

            var deviceMap = batteryDevices.ToDictionary(d => d.Id);

            foreach (var reading in latestReadings)
            {
                if (!deviceMap.TryGetValue(reading.DeviceId, out var device)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(reading.Value);
                    var level = doc.RootElement.GetProperty("battery_level").GetInt32();
                    var state = doc.RootElement.GetProperty("battery_state").GetString() ?? "unknown";

                    BatteryStatuses.Add(new BatteryStatus
                    {
                        DeviceName = device.Name,
                        BatteryLevel = level,
                        BatteryState = state,
                        LastUpdated = reading.Timestamp,
                    });
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse battery reading for DeviceId {DeviceId}", reading.DeviceId);
                }
            }

            BatteryStatuses = BatteryStatuses.OrderBy(b => b.BatteryLevel).ToList();
        }

        // Query latest connectivity reading per device to find unreachable devices
        var allDeviceIds = await _db.Devices
            .Where(d => hubIds.Contains(d.HubId))
            .Select(d => d.Id)
            .ToListAsync();

        var connectivityReadings = await _db.DeviceReadings
            .Include(r => r.Device)
            .Where(r => allDeviceIds.Contains(r.DeviceId) && r.ReadingType == ReadingTypes.ZigbeeConnectivity)
            .GroupBy(r => r.DeviceId)
            .Select(g => g.OrderByDescending(r => r.Timestamp).First())
            .AsNoTracking()
            .ToListAsync();

        foreach (var reading in connectivityReadings)
        {
            try
            {
                using var doc = JsonDocument.Parse(reading.Value);
                var status = doc.RootElement.GetProperty("status").GetString() ?? "unknown";
                if (status != "connected")
                {
                    UnreachableDevices.Add(new UnreachableDevice
                    {
                        DeviceName = reading.Device.Name,
                        Status = status,
                        LastUpdated = reading.Timestamp,
                    });
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse connectivity reading for DeviceId {DeviceId}", reading.DeviceId);
            }
        }

        UnreachableDevices = UnreachableDevices.OrderBy(d => d.DeviceName).ToList();
    }

    public class ActivityWindow
    {
        public string Label { get; set; } = string.Empty;
        public int DevicesWithMotion { get; set; }
        public int TotalMotionSensors { get; set; }
        public int TotalMotionEvents { get; set; }
        public string? LatestMotionSensor { get; set; }
        public double? TemperatureMin { get; set; }
        public double? TemperatureMedian { get; set; }
        public double? TemperatureMax { get; set; }
    }

    public class BatteryStatus
    {
        public string DeviceName { get; set; } = string.Empty;
        public int BatteryLevel { get; set; }
        public string BatteryState { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
    }

    public class UnreachableDevice
    {
        public string DeviceName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
    }
}
