namespace Hpoll.Worker.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Hpoll.Core.Interfaces;
using Hpoll.Data;
using Hpoll.Data.Entities;

/// <summary>
/// Persists key-value system information entries to the database. Used by background
/// services to record runtime stats displayed on the admin About page.
/// </summary>
public class SystemInfoService : ISystemInfoService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SystemInfoService> _logger;
    private readonly TimeProvider _timeProvider;

    public SystemInfoService(
        IServiceScopeFactory scopeFactory,
        ILogger<SystemInfoService> logger,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task SetAsync(string category, string key, string value, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();

        var entry = await db.SystemInfo.FindAsync(new object[] { key }, ct);
        if (entry == null)
        {
            entry = new SystemInfo { Key = key, Category = category, Value = value, UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime };
            db.SystemInfo.Add(entry);
        }
        else
        {
            entry.Value = value;
            entry.Category = category;
            entry.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task SetBatchAsync(string category, Dictionary<string, string> entries, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();

        var keys = entries.Keys.ToList();
        var existing = await db.SystemInfo
            .Where(e => keys.Contains(e.Key))
            .ToDictionaryAsync(e => e.Key, ct);

        foreach (var (key, value) in entries)
        {
            if (existing.TryGetValue(key, out var entry))
            {
                entry.Value = value;
                entry.Category = category;
                entry.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            }
            else
            {
                db.SystemInfo.Add(new SystemInfo
                {
                    Key = key,
                    Category = category,
                    Value = value,
                    UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task ClearAllAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM SystemInfo", ct);
    }
}
