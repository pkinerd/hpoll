using Microsoft.EntityFrameworkCore;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Core.Services;
using Hpoll.Data;
using Hpoll.Email;
using Hpoll.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configuration binding
builder.Services.Configure<HpollSettings>(builder.Configuration);
builder.Services.Configure<PollingSettings>(builder.Configuration.GetSection("Polling"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<HueAppSettings>(builder.Configuration.GetSection("HueApp"));

// Database
var dbPath = Path.Combine(
    builder.Configuration.GetValue<string>("DataPath") ?? "data",
    "hpoll.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<HpollDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// HTTP client for Hue API
builder.Services.AddHttpClient("HueApi");
builder.Services.AddScoped<IHueApiClient, HueApiClient>();

// Services
builder.Services.AddScoped<ConfigSeeder>();
builder.Services.AddScoped<HealthEvaluator>();
builder.Services.AddScoped<IEmailRenderer, EmailRenderer>();
builder.Services.AddScoped<IEmailSender, SesEmailSender>();

// Background services
builder.Services.AddHostedService<PollingService>();
builder.Services.AddHostedService<TokenRefreshService>();
builder.Services.AddHostedService<EmailSchedulerService>();

var host = builder.Build();

// Initialize DB and seed config
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();
    await db.Database.MigrateAsync();

    var settings = builder.Configuration.Get<HpollSettings>();
    if (settings?.Customers.Count > 0)
    {
        var seeder = scope.ServiceProvider.GetRequiredService<ConfigSeeder>();
        await seeder.SeedAsync(settings.Customers);
    }
}

await host.RunAsync();
