namespace Hpoll.Worker.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Data;

public class EmailSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailSchedulerService> _logger;
    private readonly EmailSettings _settings;

    public EmailSchedulerService(
        IServiceScopeFactory scopeFactory,
        ILogger<EmailSchedulerService> logger,
        IOptions<EmailSettings> settings)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email scheduler started. Send time: {Time} UTC", _settings.SendTimeUtc);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var sendTime = ParseSendTime(now);

            // If send time has already passed today, schedule for tomorrow
            if (sendTime <= now)
            {
                sendTime = sendTime.AddDays(1);
            }

            var delay = sendTime - now;
            _logger.LogInformation("Next email batch scheduled for {Time} (in {Delay})", sendTime, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
                await SendAllEmailsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in email scheduler");
                // Wait a bit before retrying to avoid tight loop on persistent errors
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private DateTime ParseSendTime(DateTime today)
    {
        if (TimeSpan.TryParse(_settings.SendTimeUtc, out var time))
        {
            return today.Date.Add(time);
        }
        _logger.LogWarning("Failed to parse SendTimeUtc '{Value}', defaulting to 08:00 UTC", _settings.SendTimeUtc);
        return today.Date.AddHours(8);
    }

    private async Task SendAllEmailsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();
        var renderer = scope.ServiceProvider.GetRequiredService<IEmailRenderer>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var customers = await db.Customers
            .Where(c => c.Status == "active")
            .ToListAsync(ct);

        var yesterday = DateTime.UtcNow.Date.AddDays(-1);

        _logger.LogInformation("Sending daily summary emails for {Count} customers, date: {Date}", customers.Count, yesterday);

        foreach (var customer in customers)
        {
            try
            {
                var html = await renderer.RenderDailySummaryAsync(customer.Id, yesterday, ct);
                var subject = $"hpoll Daily Summary - {yesterday:d MMM yyyy}";
                await sender.SendEmailAsync(customer.Email, subject, html, ct);

                _logger.LogInformation("Email sent to {Email}", customer.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", customer.Email);
            }
        }
    }
}
