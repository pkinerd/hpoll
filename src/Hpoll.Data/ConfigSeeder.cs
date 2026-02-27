namespace Hpoll.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Hpoll.Core.Configuration;
using Hpoll.Data.Entities;

public class ConfigSeeder
{
    private readonly HpollDbContext _db;
    private readonly ILogger<ConfigSeeder> _logger;

    public ConfigSeeder(HpollDbContext db, ILogger<ConfigSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(List<CustomerConfig> customers, CancellationToken ct = default)
    {
        foreach (var config in customers)
        {
            var customer = await _db.Customers
                .Include(c => c.Hubs)
                .FirstOrDefaultAsync(c => c.Email == config.Email, ct);

            if (customer == null)
            {
                customer = new Customer
                {
                    Name = config.Name,
                    Email = config.Email,
                    TimeZoneId = config.TimeZoneId,
                };
                _db.Customers.Add(customer);
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Created customer: {Name} ({Email})", config.Name, config.Email);
            }
            else
            {
                customer.Name = config.Name;
                customer.TimeZoneId = config.TimeZoneId;
                customer.UpdatedAt = DateTime.UtcNow;
            }

            foreach (var hubConfig in config.Hubs)
            {
                var hub = customer.Hubs.FirstOrDefault(h => h.HueBridgeId == hubConfig.BridgeId);
                if (hub == null)
                {
                    hub = new Hub
                    {
                        CustomerId = customer.Id,
                        HueBridgeId = hubConfig.BridgeId,
                        HueApplicationKey = hubConfig.HueApplicationKey,
                        AccessToken = hubConfig.AccessToken,
                        RefreshToken = hubConfig.RefreshToken,
                        TokenExpiresAt = hubConfig.TokenExpiresAt,
                    };
                    _db.Hubs.Add(hub);
                    _logger.LogInformation("Created hub: {BridgeId} for customer {Name}", hubConfig.BridgeId, config.Name);
                }
                else
                {
                    // Only update tokens from config if DB tokens haven't been refreshed
                    // (i.e., only seed initial values; refreshed tokens in DB take precedence)
                    if (hub.AccessToken == string.Empty)
                    {
                        hub.AccessToken = hubConfig.AccessToken;
                        hub.RefreshToken = hubConfig.RefreshToken;
                        hub.TokenExpiresAt = hubConfig.TokenExpiresAt;
                    }
                    hub.HueApplicationKey = hubConfig.HueApplicationKey;
                    hub.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync(ct);
        }
    }
}
