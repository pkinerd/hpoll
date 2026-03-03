using Microsoft.Extensions.Logging;

namespace Hpoll.Core.Interfaces;

public static class SystemInfoServiceExtensions
{
    /// <summary>
    /// Sets multiple system info entries, swallowing any exceptions with a warning log.
    /// Use this for non-critical metric updates where failure should not interrupt the calling service.
    /// </summary>
    public static async Task TrySetBatchAsync(this ISystemInfoService systemInfo,
        string category, Dictionary<string, string> entries, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            await systemInfo.SetBatchAsync(category, entries, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update system info metrics");
        }
    }
}
