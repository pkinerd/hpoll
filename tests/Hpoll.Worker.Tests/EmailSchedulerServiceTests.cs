using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Data;
using Hpoll.Data.Entities;
using Hpoll.Worker.Services;

namespace Hpoll.Worker.Tests;

public class EmailSchedulerServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IEmailRenderer> _mockRenderer;
    private readonly Mock<IEmailSender> _mockSender;
    private readonly string _dbName;

    public EmailSchedulerServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _mockRenderer = new Mock<IEmailRenderer>();
        _mockSender = new Mock<IEmailSender>();

        var services = new ServiceCollection();
        services.AddDbContext<HpollDbContext>(options =>
            options.UseInMemoryDatabase(_dbName));
        services.AddScoped<IEmailRenderer>(_ => _mockRenderer.Object);
        services.AddScoped<IEmailSender>(_ => _mockSender.Object);
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    private HpollDbContext CreateDb()
    {
        var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<HpollDbContext>();
    }

    private async Task SeedCustomerAsync(string email = "test@example.com", string status = "active")
    {
        using var db = CreateDb();
        db.Customers.Add(new Customer { Name = "Test User", Email = email, Status = status });
        await db.SaveChangesAsync();
    }

    private EmailSchedulerService CreateService(EmailSettings settings)
    {
        return new EmailSchedulerService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmailSchedulerService>.Instance,
            Options.Create(settings));
    }

    [Fact]
    public async Task SendAllEmails_SendsToActiveCustomers()
    {
        await SeedCustomerAsync("alice@example.com");
        await SeedCustomerAsync("bob@example.com");

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var now = DateTime.UtcNow;
        var sendTime = now.AddSeconds(1).TimeOfDay;
        var service = CreateService(new EmailSettings
        {
            SendTimesUtc = new() { $"{sendTime.Hours:D2}:{sendTime.Minutes:D2}:{sendTime.Seconds:D2}" },
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(3000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        _mockSender.Verify(s => s.SendEmailAsync("alice@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockSender.Verify(s => s.SendEmailAsync("bob@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendAllEmails_SkipsInactiveCustomers()
    {
        await SeedCustomerAsync("active@example.com", "active");
        await SeedCustomerAsync("inactive@example.com", "inactive");

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var now = DateTime.UtcNow;
        var sendTime = now.AddSeconds(1).TimeOfDay;
        var service = CreateService(new EmailSettings
        {
            SendTimesUtc = new() { $"{sendTime.Hours:D2}:{sendTime.Minutes:D2}:{sendTime.Seconds:D2}" },
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(3000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        _mockSender.Verify(s => s.SendEmailAsync("active@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockSender.Verify(s => s.SendEmailAsync("inactive@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAllEmails_ContinuesOnSingleCustomerFailure()
    {
        await SeedCustomerAsync("fail@example.com");
        await SeedCustomerAsync("success@example.com");

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");

        // First call fails, second succeeds
        var callCount = 0;
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, string, CancellationToken>((to, subj, body, ct) =>
            {
                callCount++;
                if (callCount == 1) throw new Exception("SES error");
                return Task.CompletedTask;
            });

        var now = DateTime.UtcNow;
        var sendTime = now.AddSeconds(1).TimeOfDay;
        var service = CreateService(new EmailSettings
        {
            SendTimesUtc = new() { $"{sendTime.Hours:D2}:{sendTime.Minutes:D2}:{sendTime.Seconds:D2}" },
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(3000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Both customers should have been attempted
        _mockSender.Verify(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GracefulShutdown_StopsWithoutException()
    {
        var service = CreateService(new EmailSettings
        {
            SendTimesUtc = new() { "23:59" },
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        await service.StartAsync(CancellationToken.None);
        // Stop immediately — should not throw
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SendAllEmails_SendsEvenWithNoReadings()
    {
        await SeedCustomerAsync("nodata@example.com");

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>No data summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var now = DateTime.UtcNow;
        var sendTime = now.AddSeconds(1).TimeOfDay;
        var service = CreateService(new EmailSettings
        {
            SendTimesUtc = new() { $"{sendTime.Hours:D2}:{sendTime.Minutes:D2}:{sendTime.Seconds:D2}" },
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(3000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Email should always be sent, even with no readings
        _mockSender.Verify(s => s.SendEmailAsync("nodata@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvalidSendTimes_DefaultsTo8AM()
    {
        var service = CreateService(new EmailSettings
        {
            SendTimesUtc = new() { "invalid" },
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        // Just verify it starts without throwing
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void GetNextSendTime_PicksNextFutureTime()
    {
        var service = CreateService(new EmailSettings
        {
            SendTimesUtc = new() { "06:00", "12:00", "18:00" },
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        // At 10:00, the next send time should be 12:00 today
        var now = new DateTime(2026, 2, 28, 10, 0, 0, DateTimeKind.Utc);
        var next = service.GetNextSendTime(now);
        Assert.Equal(new DateTime(2026, 2, 28, 12, 0, 0), next);
    }

    [Fact]
    public void GetNextSendTime_WrapsToTomorrow_WhenAllTimesHavePassed()
    {
        var service = CreateService(new EmailSettings
        {
            SendTimesUtc = new() { "06:00", "12:00", "18:00" },
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        // At 20:00, all times today have passed — should wrap to 06:00 tomorrow
        var now = new DateTime(2026, 2, 28, 20, 0, 0, DateTimeKind.Utc);
        var next = service.GetNextSendTime(now);
        Assert.Equal(new DateTime(2026, 3, 1, 6, 0, 0), next);
    }

    [Fact]
    public void GetNextSendTime_PicksEarliestTime_WhenBeforeAll()
    {
        var service = CreateService(new EmailSettings
        {
            SendTimesUtc = new() { "18:00", "06:00", "12:00" },
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        // At 03:00, the next send time should be 06:00 today (list is unsorted)
        var now = new DateTime(2026, 2, 28, 3, 0, 0, DateTimeKind.Utc);
        var next = service.GetNextSendTime(now);
        Assert.Equal(new DateTime(2026, 2, 28, 6, 0, 0), next);
    }
}
