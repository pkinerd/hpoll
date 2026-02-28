namespace Hpoll.Core.Services;

using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;

public class HealthEvaluator
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _maxSilence;

    public HealthEvaluator(IOptions<PollingSettings> settings)
    {
        _failureThreshold = settings.Value.HealthFailureThreshold;
        _maxSilence = TimeSpan.FromHours(settings.Value.HealthMaxSilenceHours);
    }

    public bool IsHubHealthy(int consecutiveFailures) => consecutiveFailures < _failureThreshold;

    public bool NeedsAttention(DateTime? lastSuccessAt, int consecutiveFailures)
    {
        if (consecutiveFailures >= _failureThreshold) return true;
        if (lastSuccessAt.HasValue && DateTime.UtcNow - lastSuccessAt.Value > _maxSilence) return true;
        return false;
    }
}
