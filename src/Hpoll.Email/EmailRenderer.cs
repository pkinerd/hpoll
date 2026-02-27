namespace Hpoll.Email;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Hpoll.Core.Interfaces;
using Hpoll.Data;
using Hpoll.Data.Entities;
using System.Text;
using System.Text.Json;

public class EmailRenderer : IEmailRenderer
{
    private readonly HpollDbContext _db;
    private readonly ILogger<EmailRenderer> _logger;

    public EmailRenderer(HpollDbContext db, ILogger<EmailRenderer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<string> RenderDailySummaryAsync(int customerId, string timeZoneId, DateTime? nowUtc = null, CancellationToken ct = default)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var effectiveNowUtc = nowUtc ?? DateTime.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(effectiveNowUtc, tz);

        // Query window is simply now minus 32 hours in absolute UTC time —
        // no timezone handling needed for the data query itself
        var startUtc = effectiveNowUtc.AddHours(-32);
        var endUtc = effectiveNowUtc;

        // Snap to the end of the current 4-hour window so it's always included
        var bucketEndLocal = nowLocal.Date.AddHours(nowLocal.Hour / 4 * 4 + 4);

        // 7 windows of 4 hours each, covering the 28 hours ending at bucketEndLocal
        var bucketStartLocal = bucketEndLocal.AddHours(-28);

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
            .Where(r => deviceIds.Contains(r.DeviceId) && r.Timestamp >= startUtc && r.Timestamp < endUtc)
            .ToListAsync(ct);

        if (readings.Count == 0)
        {
            _logger.LogInformation(
                "No readings found for customer {CustomerId} in 32h window {Start} to {End} (UTC)",
                customerId, startUtc, endUtc);
        }

        // Count motion sensors specifically (not all devices)
        var motionSensorCount = await _db.Devices
            .Where(d => hubIds.Contains(d.HubId) && d.DeviceType == "motion_sensor")
            .CountAsync(ct);

        // Build 7 x 4-hour window summaries using fixed local-time boundaries
        var windows = new List<WindowSummary>();
        for (int i = 0; i < 7; i++)
        {
            var windowStartLocal = bucketStartLocal.AddHours(i * 4);
            var windowEndLocal = windowStartLocal.AddHours(4);
            var windowStartUtc = TimeZoneInfo.ConvertTimeToUtc(windowStartLocal, tz);
            var windowEndUtc = TimeZoneInfo.ConvertTimeToUtc(windowEndLocal, tz);

            var windowReadings = readings.Where(r => r.Timestamp >= windowStartUtc && r.Timestamp < windowEndUtc).ToList();

            var motionReadings = windowReadings.Where(r => r.ReadingType == "motion").ToList();
            var tempReadings = windowReadings.Where(r => r.ReadingType == "temperature").ToList();

            // Count distinct devices with motion detected
            var devicesWithMotion = motionReadings
                .Where(r => {
                    try { using var j = JsonDocument.Parse(r.Value); return j.RootElement.GetProperty("motion").GetBoolean(); }
                    catch { return false; }
                })
                .Select(r => r.DeviceId)
                .Distinct()
                .Count();

            var totalMotionEvents = motionReadings
                .Count(r => {
                    try { using var j = JsonDocument.Parse(r.Value); return j.RootElement.GetProperty("motion").GetBoolean(); }
                    catch { return false; }
                });

            // Temperature stats
            var temperatures = tempReadings
                .Select(r => {
                    try { using var j = JsonDocument.Parse(r.Value); return (double?)j.RootElement.GetProperty("temperature").GetDouble(); }
                    catch { return null; }
                })
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .OrderBy(t => t)
                .ToList();

            windows.Add(new WindowSummary
            {
                Label = $"{windowStartLocal:HH:mm}\u2013{windowEndLocal:HH:mm}",
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

        return BuildHtml(bucketStartLocal, bucketEndLocal, tzAbbrev, windows);
    }

    private static string BuildHtml(DateTime startLocal, DateTime endLocal, string tzName, List<WindowSummary> windows)
    {
        // Determine max motion events for relative bar sizing
        var maxMotion = windows.Max(w => w.TotalMotionEvents);
        if (maxMotion == 0) maxMotion = 1;

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

        // Motion activity bars
        sb.AppendLine("<table width=\"100%\" cellpadding=\"4\" cellspacing=\"0\" style=\"margin-bottom:20px;\">");
        sb.AppendLine("<tr><td colspan=\"2\" style=\"font-size:13px;font-weight:bold;color:#555;padding-bottom:8px;\">Motion Activity</td></tr>");
        foreach (var w in windows)
        {
            var pct = maxMotion > 0 ? (int)(100.0 * w.TotalMotionEvents / maxMotion) : 0;
            var color = pct > 66 ? "#e74c3c" : pct > 33 ? "#f39c12" : "#27ae60";
            sb.AppendLine($"<tr><td style=\"font-size:12px;color:#777;width:90px;white-space:nowrap;\">{w.Label}</td>");
            sb.AppendLine($"<td><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tr>");
            sb.AppendLine($"<td style=\"background-color:{color};width:{Math.Max(pct, 2)}%;height:16px;border-radius:3px;\"></td>");
            sb.AppendLine($"<td style=\"width:{100 - Math.Max(pct, 2)}%;\"></td>");
            sb.AppendLine("</tr></table></td></tr>");
        }
        sb.AppendLine("</table>");

        // Location diversity blocks
        sb.AppendLine("<table width=\"100%\" cellpadding=\"4\" cellspacing=\"0\" style=\"margin-bottom:20px;\">");
        sb.AppendLine("<tr><td colspan=\"2\" style=\"font-size:13px;font-weight:bold;color:#555;padding-bottom:8px;\">Location Diversity</td></tr>");
        foreach (var w in windows)
        {
            var total = Math.Max(w.TotalMotionSensors, 1);
            var active = Math.Min(w.DevicesWithMotion, total);
            sb.AppendLine($"<tr><td style=\"font-size:12px;color:#777;width:90px;white-space:nowrap;\">{w.Label}</td>");
            sb.Append("<td>");
            for (int i = 0; i < total && i < 10; i++)
            {
                var blockColor = i < active ? "#3498db" : "#ecf0f1";
                sb.Append($"<span style=\"display:inline-block;width:16px;height:16px;background-color:{blockColor};border-radius:3px;margin-right:3px;\"></span>");
            }
            sb.AppendLine($" <span style=\"font-size:11px;color:#999;\">{active}/{total}</span></td></tr>");
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
                sb.AppendLine("<td colspan=\"3\" style=\"text-align:center;font-size:12px;color:#ccc;\">No data</td>");
            }
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</table>");

        sb.AppendLine("</td></tr>");

        // Summary data table
        sb.AppendLine("<tr><td style=\"padding:20px;border-top:1px solid #eee;\">");
        sb.AppendLine("<h2 style=\"margin:0 0 15px;font-size:16px;color:#2c3e50;\">Summary Data</h2>");
        sb.AppendLine("<table width=\"100%\" cellpadding=\"6\" cellspacing=\"0\" style=\"font-size:12px;border-collapse:collapse;\">");
        sb.AppendLine("<tr style=\"background-color:#f8f9fa;\"><th style=\"text-align:left;border-bottom:2px solid #dee2e6;\">Window</th><th style=\"text-align:center;border-bottom:2px solid #dee2e6;\">Areas Active</th><th style=\"text-align:center;border-bottom:2px solid #dee2e6;\">Motion Events</th><th style=\"text-align:center;border-bottom:2px solid #dee2e6;\">Temp Low</th><th style=\"text-align:center;border-bottom:2px solid #dee2e6;\">Temp Med</th><th style=\"text-align:center;border-bottom:2px solid #dee2e6;\">Temp High</th></tr>");
        foreach (var w in windows)
        {
            sb.AppendLine($"<tr><td style=\"border-bottom:1px solid #eee;\">{w.Label}</td>");
            sb.AppendLine($"<td style=\"text-align:center;border-bottom:1px solid #eee;\">{w.DevicesWithMotion}/{w.TotalMotionSensors}</td>");
            sb.AppendLine($"<td style=\"text-align:center;border-bottom:1px solid #eee;\">{w.TotalMotionEvents}</td>");
            sb.AppendLine($"<td style=\"text-align:center;border-bottom:1px solid #eee;\">{(w.TemperatureMin.HasValue ? $"{w.TemperatureMin:F1}" : "\u2014")}</td>");
            sb.AppendLine($"<td style=\"text-align:center;border-bottom:1px solid #eee;\">{(w.TemperatureMedian.HasValue ? $"{w.TemperatureMedian:F1}" : "\u2014")}</td>");
            sb.AppendLine($"<td style=\"text-align:center;border-bottom:1px solid #eee;\">{(w.TemperatureMax.HasValue ? $"{w.TemperatureMax:F1}" : "\u2014")}</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</table>");
        sb.AppendLine("</td></tr>");

        // Footer
        sb.AppendLine("<tr><td style=\"background-color:#f8f9fa;padding:15px;text-align:center;font-size:11px;color:#999;\">");
        sb.AppendLine("This is an automated summary from hpoll. No individual device or location details are included for privacy.");
        sb.AppendLine("</td></tr>");

        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }

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
}
