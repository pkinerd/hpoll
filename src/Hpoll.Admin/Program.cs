using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Core.Services;
using Hpoll.Data;

var builder = WebApplication.CreateBuilder(args);

// Configuration binding
builder.Services.Configure<HueAppSettings>(builder.Configuration.GetSection("HueApp"));
builder.Services.Configure<PollingSettings>(builder.Configuration.GetSection("Polling"));

// Database — same SQLite path as the worker
var dbPath = Path.Combine(
    builder.Configuration.GetValue<string>("DataPath") ?? "data",
    "hpoll.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<HpollDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// HTTP client for Hue API
var pollingSettings = builder.Configuration.GetSection("Polling").Get<PollingSettings>() ?? new PollingSettings();
builder.Services.AddHttpClient("HueApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(pollingSettings.HttpTimeoutSeconds);
});
builder.Services.AddScoped<IHueApiClient, HueApiClient>();

// Authentication — simple password-based cookie auth
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
    });
builder.Services.AddAuthorization();

// Session — for OAuth CSRF tokens
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddRazorPages();

var app = builder.Build();

// Enable WAL mode
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages().RequireAuthorization();

// Logout endpoint
app.MapGet("/Logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/Login");
}).AllowAnonymous();

app.Run();
