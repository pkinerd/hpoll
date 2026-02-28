using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Pages.Customers;

public class IndexModel : PageModel
{
    private readonly HpollDbContext _db;

    public IndexModel(HpollDbContext db) => _db = db;

    public List<Customer> Customers { get; set; } = new();

    public async Task OnGetAsync()
    {
        Customers = await _db.Customers
            .Include(c => c.Hubs)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }
}
