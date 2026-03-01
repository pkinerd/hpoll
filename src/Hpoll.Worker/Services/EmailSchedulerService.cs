namespace Hpoll.Worker.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Core.Services;
using Hpoll.Data;
using Hpoll.Data.Entities;

public class EmailSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailSchedulerService> _logger;
    private readonly EmailSettings _settings;
    private readonly ISystemInfoService _systemInfo;
    private readonly TimeProvider _timeProvider;
    private int _totalEmailsSent;
    internal static readonly TimeSpan MaxSleepDuration = TimeSpan.FromMinutes(10);

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
        _logger.LogInformation("Email scheduler started. Default send times: {Times} UTC",
            string.Join(", ", _settings.SendTimesUtc));

        // Initialize NextSendTimeUtc for any customers that don't have one
        await InitializeNextSendTimesAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Process all due customers, then re-check for any that became due during processing
                bool sentAny;
                do
                {
                    sentAny = await ProcessDueCustomersAsync(stoppingToken);
                } while (sentAny && !stoppingToken.IsCancellationRequested);

                // Calculate sleep duration: min(10 minutes, time until next due customer)
                var delay = await GetSleepDurationAsync(stoppingToken);
                _logger.LogInformation("Next email check in {Delay}", delay);

                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in email scheduler");
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

    internal async Task InitializeNextSendTimesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();

        var customers = await db.Customers
            .Where(c => c.Status == "active" && c.NextSendTimeUtc == null)
            .ToListAsync(ct);

        if (customers.Count == 0) return;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var customer in customers)
        {
            customer.NextSendTimeUtc = SendTimeHelper.ComputeNextSendTimeUtc(
                customer.SendTimesLocal, customer.TimeZoneId, now, _settings.SendTimesUtc);
            _logger.LogInformation("Initialized NextSendTimeUtc for customer {Name} (Id={Id}): {NextSend}",
                customer.Name, customer.Id, customer.NextSendTimeUtc);
        }

        await db.SaveChangesAsync(ct);
    }

    internal async Task<bool> ProcessDueCustomersAsync(CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();
        var renderer = scope.ServiceProvider.GetRequiredService<IEmailRenderer>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var dueCustomers = await db.Customers
            .Where(c => c.Status == "active" && c.NextSendTimeUtc != null && c.NextSendTimeUtc <= now)
            .ToListAsync(ct);

        if (dueCustomers.Count == 0)
            return false;

        _logger.LogInformation("Found {Count} customers due for email", dueCustomers.Count);

        foreach (var customer in dueCustomers)
        {
            try
            {
                await SendCustomerEmailAsync(customer, renderer, sender, ct);
                _totalEmailsSent++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email} (customer {Name}, Id={Id})",
                    customer.Email, customer.Name, customer.Id);
            }

            // Always advance NextSendTimeUtc even on failure, to prevent retry loops
            var sendNow = _timeProvider.GetUtcNow().UtcDateTime;
            customer.NextSendTimeUtc = SendTimeHelper.ComputeNextSendTimeUtc(
                customer.SendTimesLocal, customer.TimeZoneId, sendNow, _settings.SendTimesUtc);
            customer.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        try
        {
            var metricTime = _timeProvider.GetUtcNow().UtcDateTime;
            await _systemInfo.SetAsync("Runtime", "runtime.last_email_sent", metricTime.ToString("O"));
            await _systemInfo.SetAsync("Runtime", "runtime.total_emails_sent", _totalEmailsSent.ToString());

            var nextDue = await GetNextDueTimeAsync(ct);
            await _systemInfo.SetAsync("Runtime", "runtime.next_email_due",
                nextDue?.ToString("O") ?? "N/A");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update system info metrics");
        }

        return true;
    }

    internal async Task SendCustomerEmailAsync(Customer customer, IEmailRenderer renderer, IEmailSender sender, CancellationToken ct)
    {
        var toList = ParseEmailList(customer.Email);
        if (toList == null)
        {
            _logger.LogWarning("Customer {Name} (Id={Id}) has no valid notification email addresses, skipping",
                customer.Name, customer.Id);
            return;
        }

        var html = await renderer.RenderDailySummaryAsync(customer.Id, customer.TimeZoneId, ct: ct);

        var tz = TimeZoneInfo.FindSystemTimeZoneById(customer.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, tz);
        var subject = $"hpoll Daily Summary - {localNow:d MMM yyyy}";
        var ccList = ParseEmailList(customer.CcEmails);
        var bccList = ParseEmailList(customer.BccEmails);
        await sender.SendEmailAsync(toList, subject, html, ccList, bccList, ct);

        _logger.LogInformation("Email sent to {Email} (customer {Name}, Id={Id})",
            customer.Email, customer.Name, customer.Id);
    }

    internal async Task<TimeSpan> GetSleepDurationAsync(CancellationToken ct)
    {
        var nextDue = await GetNextDueTimeAsync(ct);
        if (nextDue == null)
            return MaxSleepDuration;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var untilNext = nextDue.Value - now;

        if (untilNext <= TimeSpan.Zero)
            return TimeSpan.Zero;

        return untilNext < MaxSleepDuration ? untilNext : MaxSleepDuration;
    }

    private async Task<DateTime?> GetNextDueTimeAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();

        return await db.Customers
            .Where(c => c.Status == "active" && c.NextSendTimeUtc != null)
            .MinAsync(c => (DateTime?)c.NextSendTimeUtc, ct);
    }

    internal static List<string>? ParseEmailList(string commaDelimited)
    {
        if (string.IsNullOrWhiteSpace(commaDelimited)) return null;
        var list = commaDelimited
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => e.Contains('@'))
            .ToList();
        return list.Count > 0 ? list : null;
    }
}
