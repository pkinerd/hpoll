using Amazon;
using Amazon.SimpleEmail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Core.Services;
using Hpoll.Data;
using Hpoll.Email;
using Hpoll.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configuration binding
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
var pollingSettings = builder.Configuration.GetSection("Polling").Get<PollingSettings>() ?? new PollingSettings();
builder.Services.AddHttpClient("HueApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(pollingSettings.HttpTimeoutSeconds);
});
builder.Services.AddScoped<IHueApiClient, HueApiClient>();

// AWS SES client (singleton — thread-safe, reuses connections)
builder.Services.AddSingleton<IAmazonSimpleEmailService>(sp =>
{
    var emailSettings = sp.GetRequiredService<IOptions<EmailSettings>>().Value;
    var region = RegionEndpoint.GetBySystemName(emailSettings.AwsRegion);
    return new AmazonSimpleEmailServiceClient(region);
});

// Services
builder.Services.AddScoped<ConfigSeeder>();
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

    var customers = builder.Configuration.GetSection("Customers").Get<List<CustomerConfig>>();
    if (customers?.Count > 0)
    {
        var seeder = scope.ServiceProvider.GetRequiredService<ConfigSeeder>();
        await seeder.SeedAsync(customers);
    }
}

await host.RunAsync();
