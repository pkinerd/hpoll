namespace Hpoll.Email;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Data;
using Hpoll.Data.Entities;
using System.Text;
using System.Text.Json;

public class EmailRenderer : IEmailRenderer
{
    private readonly HpollDbContext _db;
    private readonly ILogger<EmailRenderer> _logger;
    private readonly EmailSettings _emailSettings;

    public EmailRenderer(HpollDbContext db, ILogger<EmailRenderer> logger, IOptions<EmailSettings> emailSettings)
    {
        _db = db;
        _logger = logger;
        _emailSettings = emailSettings.Value;
    }

    public async Task<string> RenderDailySummaryAsync(int customerId, string timeZoneId, DateTime? nowUtc = null, CancellationToken ct = default)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var effectiveNowUtc = nowUtc ?? DateTime.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(effectiveNowUtc, tz);

        var windowHours = _emailSettings.SummaryWindowHours;
        var windowCount = _emailSettings.SummaryWindowCount;
        var totalHours = windowCount * windowHours;

        // Query window covers the full span plus one extra bucket for overlap
        var startUtc = effectiveNowUtc.AddHours(-(totalHours + windowHours));
        var endUtc = effectiveNowUtc;

        // Snap to the end of the current window so it's always included
        var bucketEndLocal = nowLocal.Date.AddHours(nowLocal.Hour / windowHours * windowHours + windowHours);

        var bucketStartLocal = bucketEndLocal.AddHours(-totalHours);

        // Get all devices for this customer's hubs
        var hubIds = await _db.Hubs
            .Where(h => h.CustomerId == customerId && h.Status == "active")
            .Select(h => h.Id)
            .ToListAsync(ct);

        var deviceIds = await _db.Devices
            .Where(d => hubIds.Contains(d.HubId))
            .Select(d => d.Id)
            .ToListAsync(ct);

        var readings = await _db.DeviceReadings
            .Where(r => deviceIds.Contains(r.DeviceId)
                && r.Timestamp >= startUtc && r.Timestamp < endUtc
                && (r.ReadingType == "motion" || r.ReadingType == "temperature"))
            .AsNoTracking()
            .ToListAsync(ct);

        if (readings.Count == 0)
        {
            _logger.LogInformation(
                "No readings found for customer {CustomerId} in {Hours}h window {Start} to {End} (UTC)",
                customerId, totalHours + windowHours, startUtc, endUtc);
        }

        // Count motion sensors specifically (not all devices)
        var motionSensorCount = await _db.Devices
            .Where(d => hubIds.Contains(d.HubId) && d.DeviceType == "motion_sensor")
            .CountAsync(ct);

        var windows = new List<WindowSummary>();
        for (int i = 0; i < windowCount; i++)
        {
            var windowStartLocal = bucketStartLocal.AddHours(i * windowHours);
            var windowEndLocal = windowStartLocal.AddHours(windowHours);
            var windowStartUtc = TimeZoneInfo.ConvertTimeToUtc(windowStartLocal, tz);
            var windowEndUtc = TimeZoneInfo.ConvertTimeToUtc(windowEndLocal, tz);

            var windowReadings = readings.Where(r => r.Timestamp >= windowStartUtc && r.Timestamp < windowEndUtc).ToList();

            var motionReadings = windowReadings.Where(r => r.ReadingType == "motion").ToList();
            var tempReadings = windowReadings.Where(r => r.ReadingType == "temperature").ToList();

            // Count distinct devices with motion detected
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

            // Temperature stats
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
            windows.Add(new WindowSummary
            {
                Label = $"{windowStartLocal:HH:mm}\u2013{displayEnd:HH:mm}",
                DevicesWithMotion = devicesWithMotion,
                TotalMotionSensors = motionSensorCount > 0 ? motionSensorCount : 1,
                TotalMotionEvents = totalMotionEvents,
                TemperatureMin = temperatures.Count > 0 ? temperatures.First() : null,
                TemperatureMedian = temperatures.Count > 0 ? temperatures[temperatures.Count / 2] : null,
                TemperatureMax = temperatures.Count > 0 ? temperatures.Last() : null,
            });
        }

        // Format the timezone name for display
        var tzAbbrev = tz.IsDaylightSavingTime(nowLocal) ? tz.DaylightName : tz.StandardName;

        // Query latest battery reading per device (most recent "battery" reading for each device)
        var allBatteryReadings = await _db.DeviceReadings
            .Include(r => r.Device)
            .Where(r => deviceIds.Contains(r.DeviceId) && r.ReadingType == "battery")
            .ToListAsync(ct);

        var batteryReadings = allBatteryReadings
            .GroupBy(r => r.DeviceId)
            .Select(g => g.OrderByDescending(r => r.Timestamp).First())
            .ToList();

        var batteryStatuses = new List<BatteryStatus>();
        foreach (var reading in batteryReadings)
        {
            try
            {
                using var doc = JsonDocument.Parse(reading.Value);
                var level = doc.RootElement.GetProperty("battery_level").GetInt32();
                batteryStatuses.Add(new BatteryStatus
                {
                    DeviceName = reading.Device.Name,
                    BatteryLevel = level
                });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse battery reading value for DeviceId {DeviceId}", reading.DeviceId);
            }
        }

        batteryStatuses = batteryStatuses.OrderBy(b => b.BatteryLevel).ToList();

        windows.Reverse(); // newest window first for readability
        var displayEndLocal = bucketEndLocal > nowLocal ? nowLocal : bucketEndLocal;
        return BuildHtml(bucketStartLocal, displayEndLocal, tzAbbrev, windows, batteryStatuses, _emailSettings.BatteryAlertThreshold, _emailSettings.BatteryLevelCritical, _emailSettings.BatteryLevelWarning);
    }

    private static string BuildHtml(DateTime startLocal, DateTime endLocal, string tzName, List<WindowSummary> windows, List<BatteryStatus> batteryStatuses, int batteryAlertThreshold, int batteryLevelCritical, int batteryLevelWarning)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"></head>");
        sb.AppendLine("<body style=\"margin:0;padding:20px;font-family:Arial,Helvetica,sans-serif;background-color:#f5f5f5;\">");
        sb.AppendLine("<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:600px;margin:0 auto;background-color:#ffffff;border-radius:8px;overflow:hidden;\">");

        // Header
        sb.AppendLine("<tr><td style=\"background-color:#2c3e50;color:#ffffff;padding:20px;text-align:center;\">");
        sb.AppendLine($"<h1 style=\"margin:0;font-size:22px;\">Daily Activity Summary</h1>");
        sb.AppendLine($"<p style=\"margin:5px 0 0;font-size:14px;opacity:0.8;\">{startLocal:d MMM yyyy HH:mm} \u2013 {endLocal:d MMM yyyy HH:mm} ({tzName})</p>");
        sb.AppendLine("</td></tr>");

        // Visual section
        sb.AppendLine("<tr><td style=\"padding:20px;\">");
        sb.AppendLine("<h2 style=\"margin:0 0 15px;font-size:16px;color:#2c3e50;\">Activity Overview</h2>");

        // Motion activity bars — capped at 5 events
        sb.AppendLine("<table width=\"100%\" cellpadding=\"4\" cellspacing=\"0\" style=\"margin-bottom:20px;\">");
        sb.AppendLine("<tr><td colspan=\"3\" style=\"font-size:13px;font-weight:bold;color:#555;padding-bottom:8px;\">Motion Activity</td></tr>");
        foreach (var w in windows)
        {
            var cappedEvents = Math.Min(w.TotalMotionEvents, 5);
            var pct = cappedEvents * 20;
            var color = w.TotalMotionEvents == 0 ? "#e74c3c"
                      : w.TotalMotionEvents == 1 ? "#f39c12"
                      : "#27ae60";
            var barWidth = Math.Max(pct, 10);
            var label = w.TotalMotionEvents >= 5 ? "5+" : w.TotalMotionEvents.ToString();

            sb.AppendLine($"<tr><td style=\"font-size:12px;color:#777;width:90px;white-space:nowrap;\">{w.Label}</td>");
            sb.AppendLine($"<td><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tr>");
            sb.AppendLine($"<td style=\"background-color:{color};width:{barWidth}%;height:16px;border-radius:3px;\"></td>");
            sb.AppendLine($"<td style=\"width:{100 - barWidth}%;\"></td>");
            sb.AppendLine("</tr></table></td>");
            sb.AppendLine($"<td style=\"font-size:11px;color:#777;width:24px;text-align:right;\">{label}</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</table>");

        // Location diversity bars — active/total as percentage
        sb.AppendLine("<table width=\"100%\" cellpadding=\"4\" cellspacing=\"0\" style=\"margin-bottom:20px;\">");
        sb.AppendLine("<tr><td colspan=\"3\" style=\"font-size:13px;font-weight:bold;color:#555;padding-bottom:8px;\">Location Diversity</td></tr>");
        foreach (var w in windows)
        {
            var total = Math.Max(w.TotalMotionSensors, 1);
            var active = Math.Min(w.DevicesWithMotion, total);
            var pct = (int)(100.0 * active / total);
            var color = active == 0 ? "#e74c3c"
                      : active == 1 ? "#f39c12"
                      : "#27ae60";
            var barWidth = Math.Max(pct, 10);

            sb.AppendLine($"<tr><td style=\"font-size:12px;color:#777;width:90px;white-space:nowrap;\">{w.Label}</td>");
            sb.AppendLine($"<td><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tr>");
            sb.AppendLine($"<td style=\"background-color:{color};width:{barWidth}%;height:16px;border-radius:3px;\"></td>");
            sb.AppendLine($"<td style=\"width:{100 - barWidth}%;\"></td>");
            sb.AppendLine("</tr></table></td>");
            var diversityLabel = active >= 5 ? "5+" : active.ToString();
            sb.AppendLine($"<td style=\"font-size:11px;color:#777;width:24px;text-align:right;\">{diversityLabel}</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</table>");

        // Temperature range
        sb.AppendLine("<table width=\"100%\" cellpadding=\"4\" cellspacing=\"0\">");
        sb.AppendLine("<tr><td colspan=\"4\" style=\"font-size:13px;font-weight:bold;color:#555;padding-bottom:8px;\">Temperature Range (\u00b0C)</td></tr>");
        sb.AppendLine("<tr><td style=\"font-size:11px;color:#999;\"></td><td style=\"font-size:11px;color:#999;text-align:center;\">Low</td><td style=\"font-size:11px;color:#999;text-align:center;\">Med</td><td style=\"font-size:11px;color:#999;text-align:center;\">High</td></tr>");
        foreach (var w in windows)
        {
            sb.AppendLine($"<tr><td style=\"font-size:12px;color:#777;width:90px;\">{w.Label}</td>");
            if (w.TemperatureMin.HasValue)
            {
                sb.AppendLine($"<td style=\"text-align:center;font-size:13px;color:#3498db;\">{w.TemperatureMin:F1}</td>");
                sb.AppendLine($"<td style=\"text-align:center;font-size:13px;font-weight:bold;color:#2c3e50;\">{w.TemperatureMedian:F1}</td>");
                sb.AppendLine($"<td style=\"text-align:center;font-size:13px;color:#e74c3c;\">{w.TemperatureMax:F1}</td>");
            }
            else
            {
                sb.AppendLine("<td></td><td></td><td></td>");
            }
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</table>");

        // Battery status section — only shown if any device is below the alert threshold
        if (batteryStatuses.Count > 0 && batteryStatuses.Any(b => b.BatteryLevel <= batteryAlertThreshold))
        {
            sb.AppendLine("<table width=\"100%\" cellpadding=\"4\" cellspacing=\"0\" style=\"margin-top:20px;\">");
            sb.AppendLine("<tr><td colspan=\"3\" style=\"font-size:13px;font-weight:bold;color:#555;padding-bottom:8px;\">Battery Status</td></tr>");
            foreach (var b in batteryStatuses)
            {
                var color = b.BatteryLevel <= batteryLevelCritical ? "#e74c3c"
                          : b.BatteryLevel <= batteryLevelWarning ? "#f39c12"
                          : "#27ae60";
                var barWidth = Math.Max(b.BatteryLevel, 5);

                sb.AppendLine($"<tr><td style=\"font-size:12px;color:#777;width:140px;white-space:nowrap;\">{Encode(b.DeviceName)}</td>");
                sb.AppendLine($"<td><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tr>");
                sb.AppendLine($"<td style=\"background-color:{color};width:{barWidth}%;height:16px;border-radius:3px;\"></td>");
                sb.AppendLine($"<td style=\"width:{100 - barWidth}%;\"></td>");
                sb.AppendLine("</tr></table></td>");
                sb.AppendLine($"<td style=\"font-size:11px;color:#777;width:36px;text-align:right;\">{b.BatteryLevel}%</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");
        }

        sb.AppendLine("</td></tr>");

        // Footer
        sb.AppendLine("<tr><td style=\"background-color:#f8f9fa;padding:15px;text-align:center;font-size:11px;color:#999;\">");
        sb.AppendLine("This is an automated summary from hpoll. No individual device or location details are included for privacy.");
        sb.AppendLine("</td></tr>");

        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }

    private static string Encode(string text) =>
        System.Net.WebUtility.HtmlEncode(text);

    private class WindowSummary
    {
        public string Label { get; set; } = string.Empty;
        public int DevicesWithMotion { get; set; }
        public int TotalMotionSensors { get; set; }
        public int TotalMotionEvents { get; set; }
        public double? TemperatureMin { get; set; }
        public double? TemperatureMedian { get; set; }
        public double? TemperatureMax { get; set; }
    }

    private class BatteryStatus
    {
        public string DeviceName { get; set; } = string.Empty;
        public int BatteryLevel { get; set; }
    }
}
