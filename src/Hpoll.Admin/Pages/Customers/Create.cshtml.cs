using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Hpoll.Admin.Services;
using Hpoll.Core.Constants;
using Hpoll.Core.Services;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Pages.Customers;

public class CreateModel : PageModel
{
    private readonly HpollDbContext _db;
    private readonly SendTimeDisplayService _sendTimeService;

    public CreateModel(HpollDbContext db, SendTimeDisplayService sendTimeService)
    {
        _db = db;
        _sendTimeService = sendTimeService;
    }

    [BindProperty, Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [BindProperty, Required, StringLength(500)]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string TimeZoneId { get; set; } = "Australia/Sydney";

    [BindProperty]
    public string? SendTimesLocal { get; set; }

    public string DefaultSendTimesDisplay { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        DefaultSendTimesDisplay = await _sendTimeService.GetDefaultSendTimesDisplayAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        DefaultSendTimesDisplay = await _sendTimeService.GetDefaultSendTimesDisplayAsync();

        if (!ModelState.IsValid) return Page();

        var invalidEmails = Email
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => !MailAddress.TryCreate(e, out _))
            .ToList();
        if (invalidEmails.Count > 0)
        {
            ModelState.AddModelError(nameof(Email), $"Invalid email address(es): {string.Join(", ", invalidEmails)}");
            return Page();
        }

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
            Status = CustomerStatus.Active
        };

        var effectiveDefaults = await _sendTimeService.GetEffectiveDefaultSendTimesUtcAsync();
        customer.NextSendTimeUtc = SendTimeHelper.ComputeNextSendTimeUtc(
            customer.SendTimesLocal, customer.TimeZoneId, DateTime.UtcNow, effectiveDefaults);

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        return RedirectToPage("Detail", new { id = customer.Id });
    }
}
