using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hpoll.Admin.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private static readonly ConcurrentDictionary<string, (int Count, DateTime ResetAt)> _failedAttempts = new();
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string password)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Check rate limit
        if (_failedAttempts.TryGetValue(clientIp, out var record) && record.Count >= MaxAttempts)
        {
            if (DateTime.UtcNow < record.ResetAt)
            {
                ErrorMessage = "Too many failed attempts. Please try again later.";
                return Page();
            }
            _failedAttempts.TryRemove(clientIp, out _);
        }

        var expected = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        if (string.IsNullOrEmpty(expected))
        {
            ErrorMessage = "ADMIN_PASSWORD environment variable is not configured.";
            return Page();
        }

        var passwordBytes = Encoding.UTF8.GetBytes(password ?? string.Empty);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        if (!CryptographicOperations.FixedTimeEquals(passwordBytes, expectedBytes))
        {
            // Track failed attempt
            _failedAttempts.AddOrUpdate(clientIp,
                _ => (1, DateTime.UtcNow.Add(LockoutDuration)),
                (_, existing) => (existing.Count + 1, DateTime.UtcNow.Add(LockoutDuration)));

            ErrorMessage = "Invalid password.";
            return Page();
        }

        // Clear failed attempts on success
        _failedAttempts.TryRemove(clientIp, out _);

        var claims = new List<Claim> { new(ClaimTypes.Name, "admin") };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToPage("/Index");
    }
}
