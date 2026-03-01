using System.Reflection;
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

// Time provider
builder.Services.AddSingleton(TimeProvider.System);

// Services
builder.Services.AddScoped<IEmailRenderer, EmailRenderer>();
builder.Services.AddScoped<IEmailSender, SesEmailSender>();
builder.Services.AddSingleton<ISystemInfoService, SystemInfoService>();

// Background services
builder.Services.AddHostedService<PollingService>();
builder.Services.AddHostedService<TokenRefreshService>();
builder.Services.AddHostedService<EmailSchedulerService>();

var host = builder.Build();

// Initialize DB
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();
    await db.Database.MigrateAsync();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
}

// Populate system info
{
    var systemInfo = host.Services.GetRequiredService<ISystemInfoService>();
    await systemInfo.ClearAllAsync();

    // System
    var assembly = Assembly.GetExecutingAssembly();
    var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? assembly.GetName().Version?.ToString() ?? "unknown";
    await systemInfo.SetBatchAsync("System", new Dictionary<string, string>
    {
        ["system.version"] = version,
        ["system.runtime"] = $".NET {Environment.Version}",
        ["system.os"] = Environment.OSVersion.ToString(),
        ["system.machine_name"] = Environment.MachineName,
        ["system.data_path"] = builder.Configuration.GetValue<string>("DataPath") ?? "data",
        ["system.worker_start_time"] = DateTime.UtcNow.ToString("O"),
        ["system.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production",
        ["system.hostname"] = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName,
    });

    // Build info (baked into assembly at compile time)
    var buildEntries = new Dictionary<string, string>();
    void AddBuild(string key, string value) { if (!string.IsNullOrEmpty(value)) buildEntries[key] = value; }
    AddBuild("build.branch", Hpoll.Core.BuildInfo.Branch);
    AddBuild("build.commit", Hpoll.Core.BuildInfo.ShortCommit);
    AddBuild("build.number", Hpoll.Core.BuildInfo.BuildNumber);
    AddBuild("build.run_id", Hpoll.Core.BuildInfo.RunId);
    AddBuild("build.pull_request", Hpoll.Core.BuildInfo.PullRequest);
    AddBuild("build.timestamp", Hpoll.Core.BuildInfo.Timestamp);
    AddBuild("build.source", Hpoll.Core.BuildInfo.IsCI ? "CI" : "Local");
    if (buildEntries.Count > 0)
        await systemInfo.SetBatchAsync("Build", buildEntries);

    // Polling settings
    using var scope = host.Services.CreateScope();
    var polling = scope.ServiceProvider.GetRequiredService<IOptions<PollingSettings>>().Value;
    await systemInfo.SetBatchAsync("Polling", new Dictionary<string, string>
    {
        ["polling.interval_minutes"] = polling.IntervalMinutes.ToString(),
        ["polling.battery_poll_interval_hours"] = polling.BatteryPollIntervalHours.ToString(),
        ["polling.data_retention_hours"] = polling.DataRetentionHours.ToString(),
        ["polling.http_timeout_seconds"] = polling.HttpTimeoutSeconds.ToString(),
        ["polling.token_refresh_check_hours"] = polling.TokenRefreshCheckHours.ToString(),
        ["polling.token_refresh_threshold_hours"] = polling.TokenRefreshThresholdHours.ToString(),
        ["polling.token_refresh_max_retries"] = polling.TokenRefreshMaxRetries.ToString(),
    });

    // Email settings
    var email = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;
    await systemInfo.SetBatchAsync("Email", new Dictionary<string, string>
    {
        ["email.send_times_utc"] = string.Join(", ", email.SendTimesUtc),
        ["email.aws_region"] = email.AwsRegion,
        ["email.from_address"] = email.FromAddress,
        ["email.battery_level_critical"] = email.BatteryLevelCritical.ToString(),
        ["email.battery_level_warning"] = email.BatteryLevelWarning.ToString(),
        ["email.battery_alert_threshold"] = email.BatteryAlertThreshold.ToString(),
        ["email.summary_window_hours"] = email.SummaryWindowHours.ToString(),
        ["email.summary_window_count"] = email.SummaryWindowCount.ToString(),
        ["email.error_retry_delay_minutes"] = email.ErrorRetryDelayMinutes.ToString(),
    });

    // Hue settings (non-sensitive only)
    var hueApp = scope.ServiceProvider.GetRequiredService<IOptions<HueAppSettings>>().Value;
    await systemInfo.SetBatchAsync("Hue", new Dictionary<string, string>
    {
        ["hue.app_configured"] = (!string.IsNullOrEmpty(hueApp.ClientId)).ToString(),
        ["hue.callback_url"] = hueApp.CallbackUrl,
    });

    // Runtime (initial placeholders)
    await systemInfo.SetBatchAsync("Runtime", new Dictionary<string, string>
    {
        ["runtime.total_poll_cycles"] = "0",
        ["runtime.last_poll_completed"] = "N/A",
        ["runtime.next_poll_due"] = "N/A",
        ["runtime.last_token_check"] = "N/A",
        ["runtime.next_token_check"] = "N/A",
        ["runtime.last_email_sent"] = "N/A",
        ["runtime.next_email_due"] = "N/A",
        ["runtime.total_emails_sent"] = "0",
    });
}

await host.RunAsync();
