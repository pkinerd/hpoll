namespace Hpoll.Core.Interfaces;

public interface IEmailRenderer
{
    /// <summary>
    /// Renders the daily summary email. Readings are bucketed into time windows
    /// aligned to midnight in the customer's timezone. Window size and count are
    /// configured via <c>Email:SummaryWindowHours</c> (default 4) and
    /// <c>Email:SummaryWindowCount</c> (default 7). The query fetches an extra
    /// window of data beyond the display range to catch readings that span
    /// a boundary (i.e. <c>windowCount * windowHours + windowHours</c> total hours).
    /// </summary>
    Task<string> RenderDailySummaryAsync(int customerId, string timeZoneId, DateTime? nowUtc = null, CancellationToken ct = default);
}
