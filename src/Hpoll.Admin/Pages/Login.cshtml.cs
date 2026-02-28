using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Hpoll.Admin.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string password)
    {
        var expected = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        if (string.IsNullOrEmpty(expected))
        {
            ErrorMessage = "ADMIN_PASSWORD environment variable is not configured.";
            return Page();
        }

        if (password != expected)
        {
            ErrorMessage = "Invalid password.";
            return Page();
        }

        var claims = new List<Claim> { new(ClaimTypes.Name, "admin") };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToPage("/Index");
    }
}
