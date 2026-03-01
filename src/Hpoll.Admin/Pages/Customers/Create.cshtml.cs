using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Services;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Pages.Customers;

public class CreateModel : PageModel
{
    private readonly HpollDbContext _db;
    private readonly EmailSettings _emailSettings;

    public CreateModel(HpollDbContext db, IOptions<EmailSettings> emailSettings)
    {
        _db = db;
        _emailSettings = emailSettings.Value;
    }

    [BindProperty, Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [BindProperty, Required, StringLength(500)]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string TimeZoneId { get; set; } = "Australia/Sydney";

    [BindProperty]
    public string SendTimesLocal { get; set; } = "19:30";

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

        var sendTimes = (SendTimesLocal ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(sendTimes))
        {
            var parsed = SendTimeHelper.ParseTimeSpans(sendTimes);
            if (parsed.Count == 0)
            {
                ModelState.AddModelError(nameof(SendTimesLocal), "Invalid time format. Use HH:mm (e.g., 19:30, 08:00).");
                return Page();
            }
            parsed.Sort();
            sendTimes = string.Join(", ", parsed.Select(t => $"{t:hh\\:mm}"));
        }

        var customer = new Customer
        {
            Name = Name,
            Email = Email,
            TimeZoneId = TimeZoneId,
            SendTimesLocal = sendTimes,
            Status = "active"
        };

        customer.NextSendTimeUtc = SendTimeHelper.ComputeNextSendTimeUtc(
            customer.SendTimesLocal, customer.TimeZoneId, DateTime.UtcNow, _emailSettings.SendTimesUtc);

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        return RedirectToPage("Detail", new { id = customer.Id });
    }
}
