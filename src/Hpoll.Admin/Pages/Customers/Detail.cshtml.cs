using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Pages.Customers;

public class DetailModel : PageModel
{
    private readonly HpollDbContext _db;
    private readonly HueAppSettings _hueApp;

    public DetailModel(HpollDbContext db, IOptions<HueAppSettings> hueApp)
    {
        _db = db;
        _hueApp = hueApp.Value;
    }

    public Customer Customer { get; set; } = null!;

    [BindProperty, EmailAddress]
    public string? EditEmail { get; set; }

    public string? SuccessMessage { get; set; }
    public string? OAuthUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var customer = await _db.Customers
            .Include(c => c.Hubs)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer == null) return NotFound();
        Customer = customer;
        EditEmail = customer.Email;
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateEmailAsync(int id)
    {
        var customer = await _db.Customers.Include(c => c.Hubs).FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();
        Customer = customer;

        if (!ModelState.IsValid) return Page();

        customer.Email = EditEmail!;
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        SuccessMessage = "Email updated.";
        return Page();
    }

    public async Task<IActionResult> OnPostToggleStatusAsync(int id)
    {
        var customer = await _db.Customers.Include(c => c.Hubs).FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();

        customer.Status = customer.Status == "active" ? "inactive" : "active";
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRegisterHubAsync(int id)
    {
        var customer = await _db.Customers.Include(c => c.Hubs).FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();
        Customer = customer;
        EditEmail = customer.Email;

        if (string.IsNullOrEmpty(_hueApp.ClientId) || string.IsNullOrEmpty(_hueApp.CallbackUrl))
        {
            SuccessMessage = "HueApp:ClientId and HueApp:CallbackUrl must be configured.";
            return Page();
        }

        // Generate CSRF token and store in session
        var csrfToken = Guid.NewGuid().ToString("N");
        HttpContext.Session.SetString("OAuthCsrf", csrfToken);
        HttpContext.Session.SetInt32("OAuthCustomerId", id);

        var state = $"{id}:{csrfToken}";
        OAuthUrl = $"https://api.meethue.com/v2/oauth2/authorize" +
            $"?client_id={Uri.EscapeDataString(_hueApp.ClientId)}" +
            $"&response_type=code" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&redirect_uri={Uri.EscapeDataString(_hueApp.CallbackUrl)}";

        return Page();
    }
}
