namespace Hpoll.Core.Services;

public class HealthEvaluator
{
    public bool IsHubHealthy(int consecutiveFailures) => consecutiveFailures < 3;

    public bool NeedsAttention(DateTime? lastSuccessAt, int consecutiveFailures)
    {
        if (consecutiveFailures >= 3) return true;
        if (lastSuccessAt.HasValue && DateTime.UtcNow - lastSuccessAt.Value > TimeSpan.FromHours(6)) return true;
        return false;
    }
}
