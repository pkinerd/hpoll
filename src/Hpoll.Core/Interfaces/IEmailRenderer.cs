namespace Hpoll.Core.Interfaces;

public interface IEmailRenderer
{
    /// <summary>
    /// Renders the daily summary email. Readings are bucketed into time windows
    /// aligned to <c>Email:SummaryWindowOffsetHours</c> (default 1) past each
    /// multiple of <c>Email:SummaryWindowHours</c> (default 4), e.g. 01:00, 05:00,
    /// 09:00, 13:00, 17:00, 21:00. Window count is configured via
    /// <c>Email:SummaryWindowCount</c> (default 7). The query fetches an extra
    /// window of data beyond the display range to catch readings that span
    /// a boundary (i.e. <c>windowCount * windowHours + windowHours</c> total hours).
    /// </summary>
    Task<string> RenderDailySummaryAsync(int customerId, string timeZoneId, DateTime? nowUtc = null, CancellationToken ct = default);
}
