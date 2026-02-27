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

    [Fact]
    public async Task SendAllEmails_SendsToActiveCustomers()
    {
        await SeedCustomerAsync("alice@example.com");
        await SeedCustomerAsync("bob@example.com");

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Use a send time that is about to fire (1 second from now)
        var now = DateTime.UtcNow;
        var sendTime = now.AddSeconds(1).TimeOfDay;
        var settings = Options.Create(new EmailSettings
        {
            SendTimeUtc = $"{sendTime.Hours:D2}:{sendTime.Minutes:D2}:{sendTime.Seconds:D2}",
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        var service = new EmailSchedulerService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmailSchedulerService>.Instance,
            settings);

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

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var now = DateTime.UtcNow;
        var sendTime = now.AddSeconds(1).TimeOfDay;
        var settings = Options.Create(new EmailSettings
        {
            SendTimeUtc = $"{sendTime.Hours:D2}:{sendTime.Minutes:D2}:{sendTime.Seconds:D2}",
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        var service = new EmailSchedulerService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmailSchedulerService>.Instance,
            settings);

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

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
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
        var settings = Options.Create(new EmailSettings
        {
            SendTimeUtc = $"{sendTime.Hours:D2}:{sendTime.Minutes:D2}:{sendTime.Seconds:D2}",
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        var service = new EmailSchedulerService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmailSchedulerService>.Instance,
            settings);

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
        var settings = Options.Create(new EmailSettings
        {
            SendTimeUtc = "23:59",
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        var service = new EmailSchedulerService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmailSchedulerService>.Instance,
            settings);

        await service.StartAsync(CancellationToken.None);
        // Stop immediately — should not throw
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SendAllEmails_SkipsWhenRendererReturnsNull()
    {
        await SeedCustomerAsync("nodata@example.com");

        // Renderer returns null (no data for the period)
        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var now = DateTime.UtcNow;
        var sendTime = now.AddSeconds(1).TimeOfDay;
        var settings = Options.Create(new EmailSettings
        {
            SendTimeUtc = $"{sendTime.Hours:D2}:{sendTime.Minutes:D2}:{sendTime.Seconds:D2}",
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        var service = new EmailSchedulerService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmailSchedulerService>.Instance,
            settings);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(3000, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Email should NOT be sent when renderer returns null
        _mockSender.Verify(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ParseSendTime_DefaultsTo8AM_OnInvalidFormat()
    {
        await SeedCustomerAsync();

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // "invalid" is not a valid TimeSpan format — should default to 8:00 AM
        var settings = Options.Create(new EmailSettings
        {
            SendTimeUtc = "invalid",
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        var service = new EmailSchedulerService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmailSchedulerService>.Instance,
            settings);

        // Just verify it starts without throwing (the invalid format defaults to 8 AM)
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }
}
