namespace Hpoll.Core.Interfaces;

/// <summary>
/// Key-value store for runtime system information (e.g. polling stats, service status).
/// Entries are persisted in the SystemInfo table and displayed on the admin About page.
/// </summary>
public interface ISystemInfoService
{
    /// <summary>Sets a single key-value entry under the given category.</summary>
    Task SetAsync(string category, string key, string value, CancellationToken ct = default);

    /// <summary>Sets multiple key-value entries under the given category in a single operation.</summary>
    Task SetBatchAsync(string category, Dictionary<string, string> entries, CancellationToken ct = default);

    /// <summary>Deletes all entries from the SystemInfo table.</summary>
    Task ClearAllAsync(CancellationToken ct = default);
}
