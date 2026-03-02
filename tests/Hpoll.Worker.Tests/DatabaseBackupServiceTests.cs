using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Data;
using Hpoll.Worker.Services;

namespace Hpoll.Worker.Tests;

public class DatabaseBackupServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly string _tempDir;
    private readonly string _backupDir;

    public DatabaseBackupServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"hpoll-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _backupDir = Path.Combine(_tempDir, "backups");

        var dbPath = Path.Combine(_tempDir, "hpoll.db");

        var services = new ServiceCollection();
        services.AddDbContext<HpollDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private DatabaseBackupService CreateService(BackupSettings? settings = null, Mock<ISystemInfoService>? systemInfoMock = null, TimeProvider? timeProvider = null)
    {
        var backupSettings = settings ?? new BackupSettings
        {
            IntervalHours = 24,
            RetentionCount = 7,
            SubDirectory = "backups"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataPath"] = _tempDir
            })
            .Build();

        return new DatabaseBackupService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DatabaseBackupService>.Instance,
            Options.Create(backupSettings),
            (systemInfoMock ?? new Mock<ISystemInfoService>()).Object,
            config,
            timeProvider);
    }

    [Fact]
    public async Task CreateBackupAsync_CreatesBackupFile()
    {
        var service = CreateService();

        await service.CreateBackupAsync(CancellationToken.None);

        var backupFiles = Directory.GetFiles(_backupDir, "hpoll-*.db");
        Assert.Single(backupFiles);
    }

    [Fact]
    public async Task CreateBackupAsync_FileNameMatchesTimestampPattern()
    {
        var service = CreateService();

        await service.CreateBackupAsync(CancellationToken.None);

        var backupFiles = Directory.GetFiles(_backupDir, "hpoll-*.db");
        var fileName = Path.GetFileName(backupFiles[0]);
        Assert.Matches(@"^hpoll-\d{8}-\d{6}\.db$", fileName);
    }

    [Fact]
    public async Task CreateBackupAsync_ProducesNonEmptyFile()
    {
        var service = CreateService();

        await service.CreateBackupAsync(CancellationToken.None);

        var backupFiles = Directory.GetFiles(_backupDir, "hpoll-*.db");
        var fileInfo = new FileInfo(backupFiles[0]);
        Assert.True(fileInfo.Length > 0);
    }

    [Fact]
    public async Task CreateBackupAsync_CreatesBackupDirectory()
    {
        Assert.False(Directory.Exists(_backupDir));

        var service = CreateService();
        await service.CreateBackupAsync(CancellationToken.None);

        Assert.True(Directory.Exists(_backupDir));
    }

    [Fact]
    public void PruneOldBackups_DeletesOldestFiles_WhenOverRetentionCount()
    {
        Directory.CreateDirectory(_backupDir);

        var fileNames = new[]
        {
            "hpoll-20260101-080000.db",
            "hpoll-20260102-080000.db",
            "hpoll-20260103-080000.db",
            "hpoll-20260104-080000.db",
            "hpoll-20260105-080000.db",
        };
        foreach (var name in fileNames)
            File.WriteAllText(Path.Combine(_backupDir, name), "dummy");

        var service = CreateService(new BackupSettings
        {
            RetentionCount = 3,
            SubDirectory = "backups"
        });

        service.PruneOldBackups();

        var remaining = Directory.GetFiles(_backupDir, "hpoll-*.db")
            .Select(Path.GetFileName)
            .OrderBy(f => f)
            .ToList();

        Assert.Equal(3, remaining.Count);
        Assert.Contains("hpoll-20260105-080000.db", remaining);
        Assert.Contains("hpoll-20260104-080000.db", remaining);
        Assert.Contains("hpoll-20260103-080000.db", remaining);
    }

    [Fact]
    public void PruneOldBackups_DoesNothing_WhenUnderRetentionCount()
    {
        Directory.CreateDirectory(_backupDir);

        File.WriteAllText(Path.Combine(_backupDir, "hpoll-20260101-080000.db"), "dummy");
        File.WriteAllText(Path.Combine(_backupDir, "hpoll-20260102-080000.db"), "dummy");

        var service = CreateService(new BackupSettings
        {
            RetentionCount = 5,
            SubDirectory = "backups"
        });

        service.PruneOldBackups();

        var remaining = Directory.GetFiles(_backupDir, "hpoll-*.db");
        Assert.Equal(2, remaining.Length);
    }

    [Fact]
    public void PruneOldBackups_HandlesNonExistentDirectory()
    {
        var service = CreateService();

        // Should not throw
        service.PruneOldBackups();
    }

    [Fact]
    public void HasExistingBackups_ReturnsFalse_WhenNoBackupsExist()
    {
        var service = CreateService();

        Assert.False(service.HasExistingBackups());
    }

    [Fact]
    public void HasExistingBackups_ReturnsFalse_WhenDirectoryExistsButEmpty()
    {
        Directory.CreateDirectory(_backupDir);
        var service = CreateService();

        Assert.False(service.HasExistingBackups());
    }

    [Fact]
    public void HasExistingBackups_ReturnsTrue_WhenBackupsExist()
    {
        Directory.CreateDirectory(_backupDir);
        File.WriteAllText(Path.Combine(_backupDir, "hpoll-20260101-080000.db"), "dummy");
        var service = CreateService();

        Assert.True(service.HasExistingBackups());
    }

    [Fact]
    public async Task InitializeStatsFromExistingBackups_SetsBackupCategoryStats()
    {
        Directory.CreateDirectory(_backupDir);
        File.WriteAllText(Path.Combine(_backupDir, "hpoll-20260101-080000.db"), "dummy");
        File.WriteAllText(Path.Combine(_backupDir, "hpoll-20260102-080000.db"), "dummy");
        File.WriteAllText(Path.Combine(_backupDir, "hpoll-20260103-080000.db"), "dummy");

        var systemInfoMock = new Mock<ISystemInfoService>();
        var service = CreateService(systemInfoMock: systemInfoMock);

        // Use CancellationTokenSource to stop ExecuteAsync after initialization
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        systemInfoMock.Verify(s => s.SetAsync("Backup", "backup.total_backups", "3", It.IsAny<CancellationToken>()), Times.Once);
        systemInfoMock.Verify(s => s.SetAsync("Backup", "backup.last_backup_completed", It.IsNotNull<string>(), It.IsAny<CancellationToken>()), Times.Once);
        systemInfoMock.Verify(s => s.SetAsync("Backup", "backup.next_backup_due", It.IsNotNull<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoExistingBackups_CreatesInitialBackup()
    {
        var systemInfoMock = new Mock<ISystemInfoService>();
        var service = CreateService(systemInfoMock: systemInfoMock);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await service.StartAsync(cts.Token); await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        var backupFiles = Directory.Exists(_backupDir)
            ? Directory.GetFiles(_backupDir, "hpoll-*.db")
            : Array.Empty<string>();
        Assert.Single(backupFiles);

        // Verify system info was updated after backup
        systemInfoMock.Verify(s => s.SetAsync("Backup", "backup.last_backup_completed", It.IsNotNull<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        systemInfoMock.Verify(s => s.SetAsync("Backup", "backup.total_backups", It.IsNotNull<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_SystemInfoFailure_DoesNotCrashService()
    {
        var systemInfoMock = new Mock<ISystemInfoService>();
        systemInfoMock.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("System info write failed"));

        var service = CreateService(systemInfoMock: systemInfoMock);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await service.StartAsync(cts.Token); await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Backup should still have been created despite system info failure
        var backupFiles = Directory.Exists(_backupDir)
            ? Directory.GetFiles(_backupDir, "hpoll-*.db")
            : Array.Empty<string>();
        Assert.Single(backupFiles);
    }

    [Fact]
    public async Task CreateBackupAsync_MultipleBackups_CreatesSeparateFiles()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
        var service = CreateService(timeProvider: fakeTime);

        await service.CreateBackupAsync(CancellationToken.None);
        fakeTime.Advance(TimeSpan.FromMinutes(1));
        await service.CreateBackupAsync(CancellationToken.None);

        var backupFiles = Directory.GetFiles(_backupDir, "hpoll-*.db");
        Assert.Equal(2, backupFiles.Length);
    }

    [Fact]
    public void PruneOldBackups_ExactlyAtRetentionCount_DoesNotDelete()
    {
        Directory.CreateDirectory(_backupDir);

        var fileNames = new[] { "hpoll-20260101-080000.db", "hpoll-20260102-080000.db", "hpoll-20260103-080000.db" };
        foreach (var name in fileNames)
            File.WriteAllText(Path.Combine(_backupDir, name), "dummy");

        var service = CreateService(new BackupSettings { RetentionCount = 3, SubDirectory = "backups" });
        service.PruneOldBackups();

        var remaining = Directory.GetFiles(_backupDir, "hpoll-*.db");
        Assert.Equal(3, remaining.Length);
    }

    [Fact]
    public void HasExistingBackups_IgnoresNonMatchingFiles()
    {
        Directory.CreateDirectory(_backupDir);
        File.WriteAllText(Path.Combine(_backupDir, "other-file.txt"), "dummy");
        File.WriteAllText(Path.Combine(_backupDir, "backup.db"), "dummy");

        var service = CreateService();
        Assert.False(service.HasExistingBackups());
    }

    [Fact]
    public async Task ExecuteAsync_WithExistingBackups_SkipsInitialBackup()
    {
        Directory.CreateDirectory(_backupDir);
        File.WriteAllText(Path.Combine(_backupDir, "hpoll-20260101-080000.db"), "dummy");

        var service = CreateService();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await service.StartAsync(cts.Token); await Task.Delay(1000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Should still only have the 1 pre-existing backup (no new one created)
        var backupFiles = Directory.GetFiles(_backupDir, "hpoll-*.db");
        Assert.Single(backupFiles);
    }
}
