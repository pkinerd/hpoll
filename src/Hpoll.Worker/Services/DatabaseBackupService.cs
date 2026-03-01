namespace Hpoll.Worker.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Data;

public class DatabaseBackupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseBackupService> _logger;
    private readonly BackupSettings _settings;
    private readonly ISystemInfoService _systemInfo;
    private readonly TimeProvider _timeProvider;
    private readonly string _backupDirectory;
    private int _totalBackups;

    public DatabaseBackupService(
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseBackupService> logger,
        IOptions<BackupSettings> settings,
        ISystemInfoService systemInfo,
        IConfiguration configuration,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;
        _systemInfo = systemInfo;
        _timeProvider = timeProvider ?? TimeProvider.System;

        var dataPath = configuration.GetValue<string>("DataPath") ?? "data";
        _backupDirectory = Path.Combine(dataPath, _settings.SubDirectory);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Database backup service started. Interval: {Hours}h, retention: {Count} backups, directory: {Dir}",
            _settings.IntervalHours, _settings.RetentionCount, _backupDirectory);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CreateBackupAsync(stoppingToken);
                PruneOldBackups();
                _totalBackups++;

                try
                {
                    var now = _timeProvider.GetUtcNow().UtcDateTime;
                    await _systemInfo.SetAsync("Runtime", "runtime.last_backup_completed", now.ToString("O"));
                    await _systemInfo.SetAsync("Runtime", "runtime.next_backup_due",
                        now.AddHours(_settings.IntervalHours).ToString("O"));
                    await _systemInfo.SetAsync("Runtime", "runtime.total_backups", _totalBackups.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update system info metrics");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in database backup cycle");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(_settings.IntervalHours), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task CreateBackupAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_backupDirectory);

        var timestamp = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmmss");
        var backupFileName = $"hpoll-{timestamp}.db";
        var backupPath = Path.GetFullPath(Path.Combine(_backupDirectory, backupFileName));

        _logger.LogInformation("Creating database backup: {Path}", backupPath);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();

        // VACUUM INTO requires a string literal, not a parameter — path is from configuration, not user input
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{backupPath}'", ct);
#pragma warning restore EF1002

        var fileInfo = new FileInfo(backupPath);
        _logger.LogInformation(
            "Database backup completed: {FileName} ({SizeKB:F1} KB)",
            backupFileName, fileInfo.Length / 1024.0);
    }

    internal void PruneOldBackups()
    {
        try
        {
            if (!Directory.Exists(_backupDirectory))
                return;

            var backupFiles = Directory.GetFiles(_backupDirectory, "hpoll-*.db")
                .OrderByDescending(f => f)
                .ToList();

            if (backupFiles.Count <= _settings.RetentionCount)
                return;

            var filesToDelete = backupFiles.Skip(_settings.RetentionCount).ToList();
            foreach (var file in filesToDelete)
            {
                try
                {
                    File.Delete(file);
                    _logger.LogInformation("Pruned old backup: {FileName}", Path.GetFileName(file));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old backup: {FileName}", Path.GetFileName(file));
                }
            }

            _logger.LogInformation(
                "Backup pruning complete: {Deleted} deleted, {Remaining} retained",
                filesToDelete.Count, _settings.RetentionCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup pruning failed");
        }
    }
}
