using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Hpoll.Core.Configuration;
using Hpoll.Core.Interfaces;
using Hpoll.Core.Services;
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

    private async Task SeedCustomerAsync(
        string email = "test@example.com",
        string status = "active",
        string ccEmails = "",
        string bccEmails = "",
        string sendTimesLocal = "",
        DateTime? nextSendTimeUtc = null)
    {
        using var db = CreateDb();
        db.Customers.Add(new Customer
        {
            Name = "Test User",
            Email = email,
            Status = status,
            CcEmails = ccEmails,
            BccEmails = bccEmails,
            SendTimesLocal = sendTimesLocal,
            NextSendTimeUtc = nextSendTimeUtc
        });
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
    public async Task ProcessDueCustomers_SendsToDueActiveCustomers()
    {
        var pastTime = DateTime.UtcNow.AddMinutes(-5);
        await SeedCustomerAsync("alice@example.com", nextSendTimeUtc: pastTime);
        await SeedCustomerAsync("bob@example.com", nextSendTimeUtc: pastTime);

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        var result = await service.ProcessDueCustomersAsync(CancellationToken.None);

        Assert.True(result);
        _mockSender.Verify(s => s.SendEmailAsync(It.Is<List<string>>(l => l.Contains("alice@example.com")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockSender.Verify(s => s.SendEmailAsync(It.Is<List<string>>(l => l.Contains("bob@example.com")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDueCustomers_SkipsInactiveCustomers()
    {
        var pastTime = DateTime.UtcNow.AddMinutes(-5);
        await SeedCustomerAsync("active@example.com", "active", nextSendTimeUtc: pastTime);
        await SeedCustomerAsync("inactive@example.com", "inactive", nextSendTimeUtc: pastTime);

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        await service.ProcessDueCustomersAsync(CancellationToken.None);

        _mockSender.Verify(s => s.SendEmailAsync(It.Is<List<string>>(l => l.Contains("active@example.com")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockSender.Verify(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDueCustomers_SkipsCustomersNotYetDue()
    {
        var futureTime = DateTime.UtcNow.AddHours(2);
        await SeedCustomerAsync("notdue@example.com", nextSendTimeUtc: futureTime);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        var result = await service.ProcessDueCustomersAsync(CancellationToken.None);

        Assert.False(result);
        _mockSender.Verify(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessDueCustomers_ContinuesOnSingleCustomerFailure()
    {
        var pastTime = DateTime.UtcNow.AddMinutes(-5);
        await SeedCustomerAsync("fail@example.com", nextSendTimeUtc: pastTime);
        await SeedCustomerAsync("success@example.com", nextSendTimeUtc: pastTime);

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
        await service.ProcessDueCustomersAsync(CancellationToken.None);

        // Both customers should have been attempted
        _mockSender.Verify(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessDueCustomers_AdvancesNextSendTimeAfterSend()
    {
        var pastTime = DateTime.UtcNow.AddMinutes(-5);
        await SeedCustomerAsync("test@example.com", nextSendTimeUtc: pastTime);

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(new EmailSettings { SendTimesUtc = new() { "08:00" }, FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        await service.ProcessDueCustomersAsync(CancellationToken.None);

        using var db = CreateDb();
        var customer = await db.Customers.FirstAsync();
        Assert.NotNull(customer.NextSendTimeUtc);
        Assert.True(customer.NextSendTimeUtc > DateTime.UtcNow);
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
    public async Task ProcessDueCustomers_SendsEvenWithNoReadings()
    {
        var pastTime = DateTime.UtcNow.AddMinutes(-5);
        await SeedCustomerAsync("nodata@example.com", nextSendTimeUtc: pastTime);

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>No data summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        await service.ProcessDueCustomersAsync(CancellationToken.None);

        _mockSender.Verify(s => s.SendEmailAsync(It.Is<List<string>>(l => l.Contains("nodata@example.com")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidSendTimes_DefaultsStartsWithoutException()
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
    public async Task ProcessDueCustomers_PassesCcBccFromCustomer()
    {
        var pastTime = DateTime.UtcNow.AddMinutes(-5);
        await SeedCustomerAsync("main@example.com", "active", "cc1@example.com, cc2@example.com", "bcc@example.com", nextSendTimeUtc: pastTime);

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        await service.ProcessDueCustomersAsync(CancellationToken.None);

        _mockSender.Verify(s => s.SendEmailAsync(
            It.Is<List<string>>(l => l.Contains("main@example.com")),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<List<string>?>(cc => cc != null && cc.Count == 2 && cc.Contains("cc1@example.com") && cc.Contains("cc2@example.com")),
            It.Is<List<string>?>(bcc => bcc != null && bcc.Count == 1 && bcc.Contains("bcc@example.com")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InitializeNextSendTimes_SetsNextSendForActiveCustomersWithoutOne()
    {
        await SeedCustomerAsync("customer1@example.com"); // No NextSendTimeUtc
        await SeedCustomerAsync("customer2@example.com", "inactive"); // Inactive, should be skipped

        var service = CreateService(new EmailSettings { SendTimesUtc = new() { "08:00" }, FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        await service.InitializeNextSendTimesAsync(CancellationToken.None);

        using var db = CreateDb();
        var customers = await db.Customers.ToListAsync();
        var active = customers.First(c => c.Email == "customer1@example.com");
        var inactive = customers.First(c => c.Email == "customer2@example.com");

        Assert.NotNull(active.NextSendTimeUtc);
        Assert.True(active.NextSendTimeUtc > DateTime.UtcNow);
        Assert.Null(inactive.NextSendTimeUtc);
    }

    [Fact]
    public async Task InitializeNextSendTimes_UsesCustomerLocalSendTimes()
    {
        await SeedCustomerAsync("custom@example.com", sendTimesLocal: "19:30");

        var service = CreateService(new EmailSettings { SendTimesUtc = new() { "08:00" }, FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        await service.InitializeNextSendTimesAsync(CancellationToken.None);

        using var db = CreateDb();
        var customer = await db.Customers.FirstAsync();
        Assert.NotNull(customer.NextSendTimeUtc);
    }

    [Fact]
    public async Task GetSleepDuration_ReturnsMaxWhenNoCustomers()
    {
        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        var duration = await service.GetSleepDurationAsync(CancellationToken.None);

        Assert.Equal(EmailSchedulerService.MaxSleepDuration, duration);
    }

    [Fact]
    public async Task GetSleepDuration_ReturnsTimeTillNextDue_WhenLessThanMax()
    {
        var futureTime = DateTime.UtcNow.AddMinutes(3);
        await SeedCustomerAsync("test@example.com", nextSendTimeUtc: futureTime);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        var duration = await service.GetSleepDurationAsync(CancellationToken.None);

        Assert.True(duration < EmailSchedulerService.MaxSleepDuration);
        Assert.True(duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task GetSleepDuration_CapsAtMaxWhenNextDueIsFarAway()
    {
        var futureTime = DateTime.UtcNow.AddHours(24);
        await SeedCustomerAsync("test@example.com", nextSendTimeUtc: futureTime);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        var duration = await service.GetSleepDurationAsync(CancellationToken.None);

        Assert.Equal(EmailSchedulerService.MaxSleepDuration, duration);
    }
}
