namespace Hpoll.Core.Interfaces;

public interface ISystemInfoService
{
    Task SetAsync(string category, string key, string value, CancellationToken ct = default);
    Task SetBatchAsync(string category, Dictionary<string, string> entries, CancellationToken ct = default);
    Task ClearAllAsync(CancellationToken ct = default);
}
