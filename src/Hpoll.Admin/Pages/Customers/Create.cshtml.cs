using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Pages.Customers;

public class CreateModel : PageModel
{
    private readonly HpollDbContext _db;

    public CreateModel(HpollDbContext db) => _db = db;

    [BindProperty, Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [BindProperty, Required, StringLength(500)]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string TimeZoneId { get; set; } = "Australia/Sydney";

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            ModelState.AddModelError(nameof(TimeZoneId), "Invalid timezone.");
            return Page();
        }

        var customer = new Customer
        {
            Name = Name,
            Email = Email,
            TimeZoneId = TimeZoneId,
            Status = "active"
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        return RedirectToPage("Detail", new { id = customer.Id });
    }
}
