namespace Hpoll.Worker.Services;

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Data;

/// <summary>
/// Periodically creates SQLite database backups using VACUUM INTO and prunes old
/// backup files beyond the configured retention count.
/// </summary>
public class DatabaseBackupService : BackgroundService
{
    private static readonly Regex SafePathSegment = new(@"^[a-zA-Z0-9_-][a-zA-Z0-9_\-/]{0,49}$", RegexOptions.Compiled);

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
        ValidatePathSegment(dataPath, "DataPath");
        ValidatePathSegment(_settings.SubDirectory, "Backup:SubDirectory");
        _backupDirectory = Path.Combine(dataPath, _settings.SubDirectory);
    }

    private static void ValidatePathSegment(string value, string name)
    {
        if (!SafePathSegment.IsMatch(value))
            throw new ArgumentException(
                $"Configuration value '{name}' contains disallowed characters or exceeds 50 characters. " +
                $"Only alphanumeric, hyphen, underscore, and forward slash (non-leading) are permitted. Got: '{value}'");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Database backup service started. Interval: {Hours}h, retention: {Count} backups, directory: {Dir}",
            _settings.IntervalHours, _settings.RetentionCount, _backupDirectory);

        // On startup, only back up if no backups exist yet
        if (!HasExistingBackups())
        {
            _logger.LogInformation("No existing backups found — creating initial backup");
            await RunBackupCycleAsync(stoppingToken);
        }
        else
        {
            _logger.LogInformation("Existing backups found — waiting for next scheduled cycle");
            await InitializeStatsFromExistingBackupsAsync();
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(_settings.IntervalHours), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await RunBackupCycleAsync(stoppingToken);
        }
    }

    private async Task InitializeStatsFromExistingBackupsAsync()
    {
        try
        {
            var backupFiles = Directory.GetFiles(_backupDirectory, "hpoll-*.db")
                .OrderByDescending(f => f)
                .ToList();

            _totalBackups = backupFiles.Count;

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var mostRecent = backupFiles.FirstOrDefault();
            string lastCompleted = "N/A";
            if (mostRecent != null)
            {
                var fi = new FileInfo(mostRecent);
                lastCompleted = fi.LastWriteTimeUtc.ToString("O");
            }

            await _systemInfo.SetAsync("Backup", "backup.last_backup_completed", lastCompleted);
            await _systemInfo.SetAsync("Backup", "backup.next_backup_due",
                now.AddHours(_settings.IntervalHours).ToString("O"));
            await _systemInfo.SetAsync("Backup", "backup.total_backups", _totalBackups.ToString());

            _logger.LogInformation(
                "Backup stats initialized from existing files: {Count} backups found", _totalBackups);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize backup stats from existing files");
        }
    }

    internal bool HasExistingBackups()
    {
        return Directory.Exists(_backupDirectory)
            && Directory.GetFiles(_backupDirectory, "hpoll-*.db").Length > 0;
    }

    private async Task RunBackupCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            await CreateBackupAsync(stoppingToken);
            PruneOldBackups();
            _totalBackups++;

            try
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                await _systemInfo.SetAsync("Backup", "backup.last_backup_completed", now.ToString("O"));
                await _systemInfo.SetAsync("Backup", "backup.next_backup_due",
                    now.AddHours(_settings.IntervalHours).ToString("O"));
                await _systemInfo.SetAsync("Backup", "backup.total_backups", _totalBackups.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update system info metrics");
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down — don't log as error
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in database backup cycle");
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

        // VACUUM INTO requires a string literal, not a parameter — path segments are validated
        // against a whitelist regex in the constructor (no SQL metacharacters possible)
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
