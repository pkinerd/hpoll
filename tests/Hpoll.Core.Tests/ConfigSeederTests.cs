using Hpoll.Core.Configuration;
using Hpoll.Data;
using Hpoll.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Hpoll.Core.Tests;

public class ConfigSeederTests : IDisposable
{
    private readonly HpollDbContext _db;

    public ConfigSeederTests()
    {
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new HpollDbContext(options);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private ConfigSeeder CreateSeeder()
    {
        var loggerMock = new Mock<ILogger<ConfigSeeder>>();
        return new ConfigSeeder(_db, loggerMock.Object);
    }

    [Fact]
    public async Task SeedAsync_CreatesNewCustomerAndHub()
    {
        var seeder = CreateSeeder();
        var configs = new List<CustomerConfig>
        {
            new()
            {
                Name = "Alice",
                Email = "alice@example.com",
                Hubs = new List<HubConfig>
                {
                    new()
                    {
                        BridgeId = "bridge-001",
                        HueApplicationKey = "app-key-1",
                        AccessToken = "access-1",
                        RefreshToken = "refresh-1",
                        TokenExpiresAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
                    }
                }
            }
        };

        await seeder.SeedAsync(configs);

        var customer = await _db.Customers.Include(c => c.Hubs).FirstOrDefaultAsync();
        Assert.NotNull(customer);
        Assert.Equal("Alice", customer.Name);
        Assert.Equal("alice@example.com", customer.Email);
        Assert.Single(customer.Hubs);

        var hub = customer.Hubs[0];
        Assert.Equal("bridge-001", hub.HueBridgeId);
        Assert.Equal("app-key-1", hub.HueApplicationKey);
        Assert.Equal("access-1", hub.AccessToken);
        Assert.Equal("refresh-1", hub.RefreshToken);
    }

    [Fact]
    public async Task SeedAsync_UpdatesExistingCustomerName()
    {
        // Pre-seed a customer
        _db.Customers.Add(new Customer
        {
            Name = "Old Name",
            Email = "bob@example.com"
        });
        await _db.SaveChangesAsync();

        var seeder = CreateSeeder();
        var configs = new List<CustomerConfig>
        {
            new()
            {
                Name = "New Name",
                Email = "bob@example.com",
                Hubs = new List<HubConfig>()
            }
        };

        await seeder.SeedAsync(configs);

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == "bob@example.com");
        Assert.NotNull(customer);
        Assert.Equal("New Name", customer.Name);
    }

    [Fact]
    public async Task SeedAsync_DoesNotOverwriteRefreshedTokens()
    {
        // Pre-seed a customer with a hub that already has tokens
        var customer = new Customer
        {
            Name = "Charlie",
            Email = "charlie@example.com"
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "bridge-002",
            HueApplicationKey = "old-app-key",
            AccessToken = "existing-access-token",
            RefreshToken = "existing-refresh-token",
            TokenExpiresAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        var seeder = CreateSeeder();
        var configs = new List<CustomerConfig>
        {
            new()
            {
                Name = "Charlie",
                Email = "charlie@example.com",
                Hubs = new List<HubConfig>
                {
                    new()
                    {
                        BridgeId = "bridge-002",
                        HueApplicationKey = "new-app-key",
                        AccessToken = "config-access-token",
                        RefreshToken = "config-refresh-token",
                        TokenExpiresAt = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
                    }
                }
            }
        };

        await seeder.SeedAsync(configs);

        var updatedHub = await _db.Hubs.FirstOrDefaultAsync(h => h.HueBridgeId == "bridge-002");
        Assert.NotNull(updatedHub);
        // Tokens should NOT be overwritten since AccessToken was non-empty
        Assert.Equal("existing-access-token", updatedHub.AccessToken);
        Assert.Equal("existing-refresh-token", updatedHub.RefreshToken);
        // But the application key SHOULD be updated
        Assert.Equal("new-app-key", updatedHub.HueApplicationKey);
    }

    [Fact]
    public async Task SeedAsync_AddsNewHubToExistingCustomer()
    {
        // Pre-seed a customer with one hub
        var customer = new Customer
        {
            Name = "Diana",
            Email = "diana@example.com"
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var existingHub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "bridge-existing",
            HueApplicationKey = "key-existing",
            AccessToken = "token-existing",
            RefreshToken = "refresh-existing"
        };
        _db.Hubs.Add(existingHub);
        await _db.SaveChangesAsync();

        var seeder = CreateSeeder();
        var configs = new List<CustomerConfig>
        {
            new()
            {
                Name = "Diana",
                Email = "diana@example.com",
                Hubs = new List<HubConfig>
                {
                    new()
                    {
                        BridgeId = "bridge-existing",
                        HueApplicationKey = "key-existing",
                        AccessToken = "token-existing",
                        RefreshToken = "refresh-existing"
                    },
                    new()
                    {
                        BridgeId = "bridge-new",
                        HueApplicationKey = "key-new",
                        AccessToken = "token-new",
                        RefreshToken = "refresh-new",
                        TokenExpiresAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
                    }
                }
            }
        };

        await seeder.SeedAsync(configs);

        var hubs = await _db.Hubs.Where(h => h.CustomerId == customer.Id).ToListAsync();
        Assert.Equal(2, hubs.Count);
        Assert.Contains(hubs, h => h.HueBridgeId == "bridge-existing");
        Assert.Contains(hubs, h => h.HueBridgeId == "bridge-new");
    }

    [Fact]
    public async Task SeedAsync_EmptyList_DoesNothing()
    {
        var seeder = CreateSeeder();

        await seeder.SeedAsync(new List<CustomerConfig>());

        Assert.Empty(await _db.Customers.ToListAsync());
        Assert.Empty(await _db.Hubs.ToListAsync());
    }
}
