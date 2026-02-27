namespace Hpoll.Core.Interfaces;

public interface IEmailRenderer
{
    /// <summary>
    /// Renders the daily summary email covering the last 24 hours of data.
    /// Readings are bucketed into standardised 4-hour windows aligned to midnight
    /// in the customer's timezone (00:00, 04:00, 08:00, 12:00, 16:00, 20:00).
    /// Returns null if there are no readings in that period.
    /// </summary>
    Task<string?> RenderDailySummaryAsync(int customerId, string timeZoneId, DateTime? nowUtc = null, CancellationToken ct = default);
}
