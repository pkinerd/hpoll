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
    private readonly ISystemInfoService _systemInfo;
    private readonly TimeProvider _timeProvider;
    private int _totalEmailsSent;

    public EmailSchedulerService(
        IServiceScopeFactory scopeFactory,
        ILogger<EmailSchedulerService> logger,
        IOptions<EmailSettings> settings,
        ISystemInfoService systemInfo,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = settings.Value;
        _systemInfo = systemInfo;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email scheduler started. Send times: {Times} UTC",
            string.Join(", ", _settings.SendTimesUtc));

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var nextSendTime = GetNextSendTime(now);

            var delay = nextSendTime - now;
            _logger.LogInformation("Next email batch scheduled for {Time} (in {Delay})", nextSendTime, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
                await SendAllEmailsAsync(stoppingToken);

                try
                {
                    _totalEmailsSent++;
                    var metricTime = _timeProvider.GetUtcNow().UtcDateTime;
                    await _systemInfo.SetAsync("Runtime", "runtime.last_email_sent", metricTime.ToString("O"));
                    await _systemInfo.SetAsync("Runtime", "runtime.total_emails_sent", _totalEmailsSent.ToString());
                    var nextSend = GetNextSendTime(metricTime);
                    await _systemInfo.SetAsync("Runtime", "runtime.next_email_due", nextSend.ToString("O"));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update system info metrics");
                }
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
                    await Task.Delay(TimeSpan.FromMinutes(_settings.ErrorRetryDelayMinutes), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    public DateTime GetNextSendTime(DateTime now)
    {
        var times = new List<TimeSpan>();
        foreach (var entry in _settings.SendTimesUtc)
        {
            if (TimeSpan.TryParse(entry, out var ts))
                times.Add(ts);
            else
                _logger.LogWarning("Failed to parse send time '{Value}', ignoring", entry);
        }

        if (times.Count == 0)
        {
            _logger.LogWarning("No valid send times configured, defaulting to 08:00 UTC");
            times.Add(new TimeSpan(8, 0, 0));
        }

        times.Sort();

        // Find the next send time today that is still in the future
        foreach (var ts in times)
        {
            var candidate = now.Date.Add(ts);
            if (candidate > now)
                return candidate;
        }

        // All times today have passed — use the first time tomorrow
        return now.Date.AddDays(1).Add(times[0]);
    }

    private static List<string>? ParseEmailList(string commaDelimited)
    {
        if (string.IsNullOrWhiteSpace(commaDelimited)) return null;
        var list = commaDelimited
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => e.Contains('@'))
            .ToList();
        return list.Count > 0 ? list : null;
    }

    internal async Task SendAllEmailsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();
        var renderer = scope.ServiceProvider.GetRequiredService<IEmailRenderer>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var customers = await db.Customers
            .Where(c => c.Status == "active")
            .ToListAsync(ct);

        _logger.LogInformation("Sending daily summary emails for {Count} customers", customers.Count);

        foreach (var customer in customers)
        {
            try
            {
                var toList = ParseEmailList(customer.Email);
                if (toList == null)
                {
                    _logger.LogWarning("Customer {Name} (Id={Id}) has no valid notification email addresses, skipping",
                        customer.Name, customer.Id);
                    continue;
                }

                var html = await renderer.RenderDailySummaryAsync(customer.Id, customer.TimeZoneId, ct: ct);

                var tz = TimeZoneInfo.FindSystemTimeZoneById(customer.TimeZoneId);
                var localNow = TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, tz);
                var subject = $"hpoll Daily Summary - {localNow:d MMM yyyy}";
                var ccList = ParseEmailList(customer.CcEmails);
                var bccList = ParseEmailList(customer.BccEmails);
                await sender.SendEmailAsync(toList, subject, html, ccList, bccList, ct);

                _logger.LogInformation("Email sent to {Email}", customer.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", customer.Email);
            }
        }
    }
}
