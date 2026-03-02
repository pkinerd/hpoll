using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Hpoll.Data;
using Hpoll.Worker.Services;

namespace Hpoll.Worker.Tests;

public class SystemInfoServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly string _dbName;

    public SystemInfoServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();
        services.AddDbContext<HpollDbContext>(options =>
            options.UseInMemoryDatabase(_dbName));
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    private HpollDbContext CreateDb()
    {
        var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<HpollDbContext>();
    }

    private SystemInfoService CreateService()
    {
        return new SystemInfoService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SystemInfoService>.Instance);
    }

    [Fact]
    public async Task SetAsync_CreatesNewEntry()
    {
        var service = CreateService();

        await service.SetAsync("Runtime", "test.key", "test-value");

        using var db = CreateDb();
        var entry = await db.SystemInfo.FindAsync("test.key");
        Assert.NotNull(entry);
        Assert.Equal("test-value", entry.Value);
        Assert.Equal("Runtime", entry.Category);
    }

    [Fact]
    public async Task SetAsync_UpdatesExistingEntry()
    {
        var service = CreateService();

        await service.SetAsync("Runtime", "test.key", "original-value");
        await service.SetAsync("Runtime", "test.key", "updated-value");

        using var db = CreateDb();
        var entry = await db.SystemInfo.FindAsync("test.key");
        Assert.NotNull(entry);
        Assert.Equal("updated-value", entry.Value);
    }

    [Fact]
    public async Task SetAsync_UpdatesCategoryOnExistingEntry()
    {
        var service = CreateService();

        await service.SetAsync("OldCategory", "test.key", "value");
        await service.SetAsync("NewCategory", "test.key", "value");

        using var db = CreateDb();
        var entry = await db.SystemInfo.FindAsync("test.key");
        Assert.Equal("NewCategory", entry!.Category);
    }

    [Fact]
    public async Task SetAsync_SetsUpdatedAtTimestamp()
    {
        var before = DateTime.UtcNow;
        var service = CreateService();

        await service.SetAsync("Runtime", "test.key", "value");

        using var db = CreateDb();
        var entry = await db.SystemInfo.FindAsync("test.key");
        Assert.True(entry!.UpdatedAt >= before);
    }

    [Fact]
    public async Task SetBatchAsync_CreatesMultipleNewEntries()
    {
        var service = CreateService();

        var entries = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2",
            ["key3"] = "value3"
        };

        await service.SetBatchAsync("Batch", entries);

        using var db = CreateDb();
        var allEntries = await db.SystemInfo.ToListAsync();
        Assert.Equal(3, allEntries.Count);
        Assert.All(allEntries, e => Assert.Equal("Batch", e.Category));
    }

    [Fact]
    public async Task SetBatchAsync_UpdatesExistingEntries()
    {
        var service = CreateService();

        await service.SetAsync("Old", "key1", "old-value1");
        await service.SetAsync("Old", "key2", "old-value2");

        var entries = new Dictionary<string, string>
        {
            ["key1"] = "new-value1",
            ["key2"] = "new-value2"
        };

        await service.SetBatchAsync("New", entries);

        using var db = CreateDb();
        var entry1 = await db.SystemInfo.FindAsync("key1");
        var entry2 = await db.SystemInfo.FindAsync("key2");
        Assert.Equal("new-value1", entry1!.Value);
        Assert.Equal("new-value2", entry2!.Value);
        Assert.Equal("New", entry1.Category);
    }

    [Fact]
    public async Task SetBatchAsync_MixOfNewAndExistingEntries()
    {
        var service = CreateService();

        await service.SetAsync("Old", "existing-key", "old-value");

        var entries = new Dictionary<string, string>
        {
            ["existing-key"] = "updated-value",
            ["new-key"] = "new-value"
        };

        await service.SetBatchAsync("Mixed", entries);

        using var db = CreateDb();
        var existing = await db.SystemInfo.FindAsync("existing-key");
        var newEntry = await db.SystemInfo.FindAsync("new-key");
        Assert.Equal("updated-value", existing!.Value);
        Assert.Equal("new-value", newEntry!.Value);
        Assert.Equal("Mixed", existing.Category);
        Assert.Equal("Mixed", newEntry.Category);
    }

    [Fact]
    public async Task ClearAllAsync_RemovesAllEntries()
    {
        // ClearAllAsync uses ExecuteSqlRawAsync which requires a relational provider,
        // so use SQLite in-memory instead of the EF Core InMemory provider
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var sqliteServices = new ServiceCollection();
        sqliteServices.AddDbContext<HpollDbContext>(options => options.UseSqlite(connection));
        sqliteServices.AddLogging();
        using var sqliteProvider = sqliteServices.BuildServiceProvider();

        using (var db = sqliteProvider.CreateScope().ServiceProvider.GetRequiredService<HpollDbContext>())
            db.Database.EnsureCreated();

        var service = new SystemInfoService(
            sqliteProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<SystemInfoService>.Instance);

        await service.SetAsync("Cat", "key1", "value1");
        await service.SetAsync("Cat", "key2", "value2");

        using (var db = sqliteProvider.CreateScope().ServiceProvider.GetRequiredService<HpollDbContext>())
            Assert.Equal(2, await db.SystemInfo.CountAsync());

        await service.ClearAllAsync();

        using (var db = sqliteProvider.CreateScope().ServiceProvider.GetRequiredService<HpollDbContext>())
            Assert.Equal(0, await db.SystemInfo.CountAsync());
    }

    [Fact]
    public async Task SetBatchAsync_EmptyDictionary_DoesNotThrow()
    {
        var service = CreateService();

        await service.SetBatchAsync("Cat", new Dictionary<string, string>());

        using var db = CreateDb();
        var count = await db.SystemInfo.CountAsync();
        Assert.Equal(0, count);
    }
}
