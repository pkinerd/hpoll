using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hpoll.Admin.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private static readonly ConcurrentDictionary<string, (int Count, DateTime ResetAt)> _failedAttempts = new();
    private static readonly PasswordHasher<object> _hasher = new();
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public bool IsSetupMode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? GeneratedHash { get; set; }

    public void OnGet()
    {
        IsSetupMode = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ADMIN_PASSWORD_HASH"));
    }

    public async Task<IActionResult> OnPostAsync(string password)
    {
        var storedHash = Environment.GetEnvironmentVariable("ADMIN_PASSWORD_HASH");
        if (string.IsNullOrEmpty(storedHash))
        {
            IsSetupMode = true;
            ErrorMessage = "ADMIN_PASSWORD_HASH is not configured. Use the form below to generate one.";
            return Page();
        }

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

        var result = _hasher.VerifyHashedPassword(null!, storedHash, password ?? string.Empty);
        if (result == PasswordVerificationResult.Failed)
        {
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

    public IActionResult OnPostSetup(string password, string confirmPassword)
    {
        IsSetupMode = true;

        if (string.IsNullOrEmpty(password))
        {
            ErrorMessage = "Password is required.";
            return Page();
        }

        if (password.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters.";
            return Page();
        }

        if (password != confirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return Page();
        }

        GeneratedHash = _hasher.HashPassword(null!, password);
        return Page();
    }
}
