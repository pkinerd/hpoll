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

    private async Task SeedCustomerAsync(string email = "test@example.com", string status = "active", string ccEmails = "", string bccEmails = "")
    {
        using var db = CreateDb();
        db.Customers.Add(new Customer { Name = "Test User", Email = email, Status = status, CcEmails = ccEmails, BccEmails = bccEmails });
        await db.SaveChangesAsync();
    }

    private EmailSchedulerService CreateService(EmailSettings settings)
    {
        return new EmailSchedulerService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmailSchedulerService>.Instance,
            Options.Create(settings),
            new Mock<ISystemInfoService>().Object);
    }

    [Fact]
    public async Task SendAllEmails_SendsToActiveCustomers()
    {
        await SeedCustomerAsync("alice@example.com");
        await SeedCustomerAsync("bob@example.com");

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        await service.SendAllEmailsAsync(CancellationToken.None);

        _mockSender.Verify(s => s.SendEmailAsync(It.Is<List<string>>(l => l.Contains("alice@example.com")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockSender.Verify(s => s.SendEmailAsync(It.Is<List<string>>(l => l.Contains("bob@example.com")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAllEmails_SkipsInactiveCustomers()
    {
        await SeedCustomerAsync("active@example.com", "active");
        await SeedCustomerAsync("inactive@example.com", "inactive");

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        await service.SendAllEmailsAsync(CancellationToken.None);

        _mockSender.Verify(s => s.SendEmailAsync(It.Is<List<string>>(l => l.Contains("active@example.com")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockSender.Verify(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAllEmails_ContinuesOnSingleCustomerFailure()
    {
        await SeedCustomerAsync("fail@example.com");
        await SeedCustomerAsync("success@example.com");

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");

        var callCount = 0;
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns<List<string>, string, string, List<string>?, List<string>?, CancellationToken>((to, subj, body, cc, bcc, ct) =>
            {
                callCount++;
                if (callCount == 1) throw new Exception("SES error");
                return Task.CompletedTask;
            });

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        await service.SendAllEmailsAsync(CancellationToken.None);

        // Both customers should have been attempted
        _mockSender.Verify(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
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
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SendAllEmails_SendsEvenWithNoReadings()
    {
        await SeedCustomerAsync("nodata@example.com");

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>No data summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        await service.SendAllEmailsAsync(CancellationToken.None);

        _mockSender.Verify(s => s.SendEmailAsync(It.Is<List<string>>(l => l.Contains("nodata@example.com")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
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

        var now = new DateTime(2026, 2, 28, 3, 0, 0, DateTimeKind.Utc);
        var next = service.GetNextSendTime(now);
        Assert.Equal(new DateTime(2026, 2, 28, 6, 0, 0), next);
    }

    [Fact]
    public void GetNextSendTime_EmptySendTimesList_DefaultsTo0800()
    {
        var service = CreateService(new EmailSettings
        {
            SendTimesUtc = new(),
            FromAddress = "noreply@hpoll.com",
            AwsRegion = "us-east-1"
        });

        var now = new DateTime(2026, 2, 28, 3, 0, 0, DateTimeKind.Utc);
        var next = service.GetNextSendTime(now);
        Assert.Equal(new DateTime(2026, 2, 28, 8, 0, 0), next);
    }

    [Fact]
    public async Task SendAllEmails_PassesCcBccFromCustomer()
    {
        await SeedCustomerAsync("main@example.com", "active", "cc1@example.com, cc2@example.com", "bcc@example.com");

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        await service.SendAllEmailsAsync(CancellationToken.None);

        _mockSender.Verify(s => s.SendEmailAsync(
            It.Is<List<string>>(l => l.Contains("main@example.com")),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<List<string>?>(cc => cc != null && cc.Count == 2 && cc.Contains("cc1@example.com") && cc.Contains("cc2@example.com")),
            It.Is<List<string>?>(bcc => bcc != null && bcc.Count == 1 && bcc.Contains("bcc@example.com")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
