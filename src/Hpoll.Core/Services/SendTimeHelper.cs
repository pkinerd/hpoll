namespace Hpoll.Core.Services;

public static class SendTimeHelper
{
    /// <summary>
    /// Computes the next UTC send time for a customer given their local send times and timezone.
    /// </summary>
    /// <param name="sendTimesLocal">Comma-separated local times in HH:mm format.</param>
    /// <param name="timeZoneId">IANA timezone identifier.</param>
    /// <param name="nowUtc">Current UTC time.</param>
    /// <param name="defaultSendTimesUtc">Fallback UTC send times from config (used when sendTimesLocal is empty).</param>
    /// <returns>The next send time in UTC, or null if no valid times could be parsed.</returns>
    public static DateTime? ComputeNextSendTimeUtc(
        string sendTimesLocal,
        string timeZoneId,
        DateTime nowUtc,
        List<string>? defaultSendTimesUtc = null)
    {
        if (!string.IsNullOrWhiteSpace(sendTimesLocal))
        {
            return ComputeFromLocalTimes(sendTimesLocal, timeZoneId, nowUtc);
        }

        // Fall back to default UTC times from config
        if (defaultSendTimesUtc != null && defaultSendTimesUtc.Count > 0)
        {
            return ComputeFromUtcTimes(defaultSendTimesUtc, nowUtc);
        }

        return null;
    }

    /// <summary>
    /// Parses comma-separated local times and finds the next future occurrence in UTC.
    /// </summary>
    private static DateTime? ComputeFromLocalTimes(string sendTimesLocal, string timeZoneId, DateTime nowUtc)
    {
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }

        var times = ParseTimeSpans(sendTimesLocal);
        if (times.Count == 0)
            return null;

        times.Sort();

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

        // Try each time today (in local time)
        foreach (var ts in times)
        {
            var candidateLocal = nowLocal.Date.Add(ts);
            var candidateUtc = SafeConvertToUtc(candidateLocal, tz);
            if (candidateUtc > nowUtc)
                return candidateUtc;
        }

        // All times today have passed — use the first time tomorrow
        var tomorrowLocal = nowLocal.Date.AddDays(1).Add(times[0]);
        return SafeConvertToUtc(tomorrowLocal, tz);
    }

    /// <summary>
    /// Computes the next send time from UTC-based default times.
    /// </summary>
    private static DateTime? ComputeFromUtcTimes(List<string> sendTimesUtc, DateTime nowUtc)
    {
        var times = new List<TimeSpan>();
        foreach (var entry in sendTimesUtc)
        {
            if (TimeSpan.TryParse(entry, out var ts))
                times.Add(ts);
        }

        if (times.Count == 0)
            return null;

        times.Sort();

        foreach (var ts in times)
        {
            var candidate = nowUtc.Date.Add(ts);
            if (candidate > nowUtc)
                return candidate;
        }

        return nowUtc.Date.AddDays(1).Add(times[0]);
    }

    /// <summary>
    /// Parses comma-separated HH:mm strings into TimeSpans.
    /// </summary>
    public static List<TimeSpan> ParseTimeSpans(string commaSeparatedTimes)
    {
        var result = new List<TimeSpan>();
        if (string.IsNullOrWhiteSpace(commaSeparatedTimes)) return result;

        foreach (var part in commaSeparatedTimes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TimeSpan.TryParse(part, out var ts))
                result.Add(ts);
        }
        return result;
    }

    /// <summary>
    /// Safely converts local time to UTC, handling ambiguous and invalid times during DST transitions.
    /// </summary>
    private static DateTime SafeConvertToUtc(DateTime localTime, TimeZoneInfo tz)
    {
        if (tz.IsInvalidTime(localTime))
        {
            // During spring-forward gap, advance to the valid time after the gap
            var adjustment = tz.GetAdjustmentRules()
                .FirstOrDefault(r => r.DateStart <= localTime && r.DateEnd >= localTime);
            var delta = adjustment?.DaylightDelta ?? TimeSpan.FromHours(1);
            localTime = localTime.Add(delta);
        }

        var unspecified = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
    }
}
