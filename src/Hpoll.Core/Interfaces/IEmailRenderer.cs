namespace Hpoll.Core.Interfaces;

public interface IEmailRenderer
{
    /// <summary>
    /// Renders the daily summary email covering the 24 hours ending at <paramref name="endUtc"/>.
    /// Returns null if there are no readings in that period.
    /// </summary>
    Task<string?> RenderDailySummaryAsync(int customerId, DateTime endUtc, CancellationToken ct = default);
}
