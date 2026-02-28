using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Pages;

public class IndexModel : PageModel
{
    private readonly HpollDbContext _db;

    public IndexModel(HpollDbContext db) => _db = db;

    public int ActiveCustomers { get; set; }
    public int InactiveCustomers { get; set; }
    public int ActiveHubs { get; set; }
    public int InactiveHubs { get; set; }
    public int NeedsReauthHubs { get; set; }
    public List<Hub> ExpiringTokenHubs { get; set; } = new();
    public List<Hub> FailingHubs { get; set; } = new();
    public List<PollingLog> RecentLogs { get; set; } = new();

    public async Task OnGetAsync()
    {
        ActiveCustomers = await _db.Customers.CountAsync(c => c.Status == "active");
        InactiveCustomers = await _db.Customers.CountAsync(c => c.Status == "inactive");

        ActiveHubs = await _db.Hubs.CountAsync(h => h.Status == "active");
        InactiveHubs = await _db.Hubs.CountAsync(h => h.Status == "inactive");
        NeedsReauthHubs = await _db.Hubs.CountAsync(h => h.Status == "needs_reauth");

        var threshold = DateTime.UtcNow.AddHours(48);
        ExpiringTokenHubs = await _db.Hubs
            .Include(h => h.Customer)
            .Where(h => h.Status == "active" && h.TokenExpiresAt < threshold)
            .OrderBy(h => h.TokenExpiresAt)
            .ToListAsync();

        FailingHubs = await _db.Hubs
            .Include(h => h.Customer)
            .Where(h => h.ConsecutiveFailures > 0)
            .OrderByDescending(h => h.ConsecutiveFailures)
            .ToListAsync();

        RecentLogs = await _db.PollingLogs
            .Include(l => l.Hub)
            .ThenInclude(h => h.Customer)
            .OrderByDescending(l => l.Timestamp)
            .Take(10)
            .ToListAsync();
    }
}
