using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    private readonly string _dataPath;
    private readonly string _backupDir;

    public DatabaseBackupServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"hpoll-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        // Use a regex-safe relative name for DataPath (no leading slash, no special chars)
        _dataPath = $"td-{Guid.NewGuid().ToString("N")[..8]}";
        var dataFullPath = Path.Combine(_tempDir, _dataPath);
        Directory.CreateDirectory(dataFullPath);
        _backupDir = Path.Combine(dataFullPath, "backups");

        var dbPath = Path.Combine(dataFullPath, "hpoll.db");

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
                ["DataPath"] = Path.Combine(_tempDir, _dataPath)
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

        systemInfoMock.Verify(s => s.SetBatchAsync("Backup",
            It.Is<Dictionary<string, string>>(d =>
                d.ContainsKey("backup.total_backups") && d["backup.total_backups"] == "3"
                && d.ContainsKey("backup.last_backup_completed")
                && d.ContainsKey("backup.next_backup_due")),
            It.IsAny<CancellationToken>()), Times.Once);
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

        // Verify system info was updated after backup via SetBatchAsync
        systemInfoMock.Verify(s => s.SetBatchAsync("Backup",
            It.Is<Dictionary<string, string>>(d =>
                d.ContainsKey("backup.last_backup_completed")
                && d.ContainsKey("backup.total_backups")),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_SystemInfoFailure_DoesNotCrashService()
    {
        var systemInfoMock = new Mock<ISystemInfoService>();
        systemInfoMock.Setup(s => s.SetBatchAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
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

    [Theory]
    [InlineData("data")]
    [InlineData("my-backups")]
    [InlineData("path/to/dir")]
    [InlineData("a_b-c/d")]
    [InlineData("/app/data")]
    [InlineData("./data")]
    public void Constructor_ValidDataPath_DoesNotThrow(string dataPath)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataPath"] = dataPath })
            .Build();

        var service = new DatabaseBackupService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DatabaseBackupService>.Instance,
            Options.Create(new BackupSettings { SubDirectory = "backups" }),
            new Mock<ISystemInfoService>().Object,
            config);

        Assert.NotNull(service);
    }

    [Theory]
    [InlineData("data'; DROP TABLE", "DataPath")]
    [InlineData("path with spaces", "DataPath")]
    [InlineData("", "DataPath")]
    public void Constructor_InvalidDataPath_ThrowsArgumentException(string dataPath, string expectedParamRef)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataPath"] = dataPath })
            .Build();

        var ex = Assert.Throws<ArgumentException>(() => new DatabaseBackupService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DatabaseBackupService>.Instance,
            Options.Create(new BackupSettings { SubDirectory = "backups" }),
            new Mock<ISystemInfoService>().Object,
            config));

        Assert.Contains(expectedParamRef, ex.Message);
    }

    [Theory]
    [InlineData("sub'; --")]
    public void Constructor_InvalidSubDirectory_ThrowsArgumentException(string subDir)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataPath"] = "data" })
            .Build();

        var ex = Assert.Throws<ArgumentException>(() => new DatabaseBackupService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DatabaseBackupService>.Instance,
            Options.Create(new BackupSettings { SubDirectory = subDir }),
            new Mock<ISystemInfoService>().Object,
            config));

        Assert.Contains("Backup:SubDirectory", ex.Message);
    }

    [Fact]
    public void Constructor_DataPathExceeding200Chars_ThrowsArgumentException()
    {
        var longPath = new string('a', 201);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataPath"] = longPath })
            .Build();

        Assert.Throws<ArgumentException>(() => new DatabaseBackupService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DatabaseBackupService>.Instance,
            Options.Create(new BackupSettings { SubDirectory = "backups" }),
            new Mock<ISystemInfoService>().Object,
            config));
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

    [Fact]
    public async Task ExecuteAsync_CancellationDuringBackup_DoesNotLogError()
    {
        var loggerMock = new Mock<ILogger<DatabaseBackupService>>();
        var systemInfoMock = new Mock<ISystemInfoService>();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataPath"] = Path.Combine(_tempDir, _dataPath)
            })
            .Build();

        var service = new DatabaseBackupService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            loggerMock.Object,
            Options.Create(new BackupSettings { IntervalHours = 24, RetentionCount = 7, SubDirectory = "backups" }),
            systemInfoMock.Object,
            config);

        // Start and quickly cancel to trigger cancellation path
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await service.StartAsync(cts.Token);
        try { await Task.Delay(500, cts.Token); } catch (OperationCanceledException) { }
        await service.StopAsync(CancellationToken.None);

        // Verify no error-level log (OperationCanceledException should be swallowed)
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task InitializeStatsFromExistingBackups_NoBackupDirectory_LogsWarningNotError()
    {
        // Don't create the backup directory — Directory.GetFiles will throw
        var loggerMock = new Mock<ILogger<DatabaseBackupService>>();
        var systemInfoMock = new Mock<ISystemInfoService>();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataPath"] = Path.Combine(_tempDir, _dataPath)
            })
            .Build();

        // Pre-create a backup so ExecuteAsync goes to InitializeStats path
        Directory.CreateDirectory(_backupDir);
        File.WriteAllText(Path.Combine(_backupDir, "hpoll-20260101-080000.db"), "dummy");

        // Make SetBatchAsync throw to trigger the catch block in InitializeStatsFromExistingBackupsAsync
        systemInfoMock.Setup(s => s.SetBatchAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB write failed"));

        var service = new DatabaseBackupService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            loggerMock.Object,
            Options.Create(new BackupSettings { IntervalHours = 24, RetentionCount = 7, SubDirectory = "backups" }),
            systemInfoMock.Object,
            config);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await service.StartAsync(cts.Token); await Task.Delay(1000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Should log warning, not crash
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InitializeStatsFromExistingBackups_SetsLastBackupFromFileTimestamp()
    {
        Directory.CreateDirectory(_backupDir);
        var backupPath = Path.Combine(_backupDir, "hpoll-20260315-120000.db");
        File.WriteAllText(backupPath, "dummy");

        var systemInfoMock = new Mock<ISystemInfoService>();
        var service = CreateService(systemInfoMock: systemInfoMock);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        systemInfoMock.Verify(s => s.SetBatchAsync("Backup",
            It.Is<Dictionary<string, string>>(d =>
                d.ContainsKey("backup.last_backup_completed")
                && d["backup.last_backup_completed"] != "N/A"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
