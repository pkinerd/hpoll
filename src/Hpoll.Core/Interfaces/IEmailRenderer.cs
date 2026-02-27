namespace Hpoll.Core.Interfaces;

public interface IEmailRenderer
{
    Task<string> RenderDailySummaryAsync(int customerId, DateTime date, CancellationToken ct = default);
}
