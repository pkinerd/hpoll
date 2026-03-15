using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Hpoll.Core.Configuration;
using Hpoll.Core.Constants;
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
    private readonly FakeTimeProvider _fakeTime;

    public EmailSchedulerServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();
        _mockRenderer = new Mock<IEmailRenderer>();
        _mockSender = new Mock<IEmailSender>();
        _fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero));

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
        foreach (var scope in _scopes) scope.Dispose();
        _serviceProvider.Dispose();
    }

    private readonly List<IServiceScope> _scopes = new();

    private HpollDbContext CreateDb()
    {
        var scope = _serviceProvider.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<HpollDbContext>();
    }

    private async Task SeedCustomerAsync(
        string email = "test@example.com",
        string status = CustomerStatus.Active,
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
            new Mock<ISystemInfoService>().Object,
            _fakeTime);
    }

    [Fact]
    public async Task ProcessDueCustomers_SendsToDueActiveCustomers()
    {
        var pastTime = _fakeTime.GetUtcNow().UtcDateTime.AddMinutes(-5);
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
        var pastTime = _fakeTime.GetUtcNow().UtcDateTime.AddMinutes(-5);
        await SeedCustomerAsync("active@example.com", CustomerStatus.Active, nextSendTimeUtc: pastTime);
        await SeedCustomerAsync("inactive@example.com", CustomerStatus.Inactive, nextSendTimeUtc: pastTime);

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
        var futureTime = _fakeTime.GetUtcNow().UtcDateTime.AddHours(2);
        await SeedCustomerAsync("notdue@example.com", nextSendTimeUtc: futureTime);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        var result = await service.ProcessDueCustomersAsync(CancellationToken.None);

        Assert.False(result);
        _mockSender.Verify(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessDueCustomers_ContinuesOnSingleCustomerFailure()
    {
        var pastTime = _fakeTime.GetUtcNow().UtcDateTime.AddMinutes(-5);
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
    public async Task ProcessDueCustomers_ContinuesOnRendererFailure_SkipsSender()
    {
        var pastTime = _fakeTime.GetUtcNow().UtcDateTime.AddMinutes(-5);
        await SeedCustomerAsync("fail@example.com", nextSendTimeUtc: pastTime);
        await SeedCustomerAsync("success@example.com", nextSendTimeUtc: pastTime);

        var callCount = 0;
        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns<int, string, DateTime?, CancellationToken>((id, tz, dt, ct) =>
            {
                callCount++;
                if (callCount == 1) throw new Exception("Database error during render");
                return Task.FromResult("<html>Summary</html>");
            });
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1", SendTimesUtc = new() { "08:00" } });
        await service.ProcessDueCustomersAsync(CancellationToken.None);

        // Renderer called for both customers
        Assert.Equal(2, callCount);
        // Sender only called for the second (successful) customer
        _mockSender.Verify(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Once);

        // Both customers should have their NextSendTimeUtc advanced (no retry loops)
        using var db = CreateDb();
        var customers = await db.Customers.ToListAsync();
        Assert.All(customers, c => Assert.True(c.NextSendTimeUtc > _fakeTime.GetUtcNow().UtcDateTime));
    }

    [Fact]
    public async Task ProcessDueCustomers_AdvancesNextSendTimeAfterSend()
    {
        var pastTime = _fakeTime.GetUtcNow().UtcDateTime.AddMinutes(-5);
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
        Assert.True(customer.NextSendTimeUtc > _fakeTime.GetUtcNow().UtcDateTime);
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
        var pastTime = _fakeTime.GetUtcNow().UtcDateTime.AddMinutes(-5);
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
        var pastTime = _fakeTime.GetUtcNow().UtcDateTime.AddMinutes(-5);
        await SeedCustomerAsync("main@example.com", CustomerStatus.Active, "cc1@example.com, cc2@example.com", "bcc@example.com", nextSendTimeUtc: pastTime);

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
        await SeedCustomerAsync("customer2@example.com", CustomerStatus.Inactive); // Inactive, should be skipped

        var service = CreateService(new EmailSettings { SendTimesUtc = new() { "08:00" }, FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        await service.InitializeNextSendTimesAsync(CancellationToken.None);

        using var db = CreateDb();
        var customers = await db.Customers.ToListAsync();
        var active = customers.First(c => c.Email == "customer1@example.com");
        var inactive = customers.First(c => c.Email == "customer2@example.com");

        Assert.NotNull(active.NextSendTimeUtc);
        Assert.True(active.NextSendTimeUtc > _fakeTime.GetUtcNow().UtcDateTime);
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
        var futureTime = _fakeTime.GetUtcNow().UtcDateTime.AddSeconds(30);
        await SeedCustomerAsync("test@example.com", nextSendTimeUtc: futureTime);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        var duration = await service.GetSleepDurationAsync(CancellationToken.None);

        Assert.True(duration < EmailSchedulerService.MaxSleepDuration);
        Assert.True(duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task GetSleepDuration_CapsAtMaxWhenNextDueIsFarAway()
    {
        var futureTime = _fakeTime.GetUtcNow().UtcDateTime.AddHours(24);
        await SeedCustomerAsync("test@example.com", nextSendTimeUtc: futureTime);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        var duration = await service.GetSleepDurationAsync(CancellationToken.None);

        Assert.Equal(EmailSchedulerService.MaxSleepDuration, duration);
    }

    [Fact]
    public async Task GetSleepDuration_ReturnsZero_WhenNextDueIsInPast()
    {
        var pastTime = _fakeTime.GetUtcNow().UtcDateTime.AddMinutes(-5);
        await SeedCustomerAsync("test@example.com", nextSendTimeUtc: pastTime);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        var duration = await service.GetSleepDurationAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.Zero, duration);
    }

    [Fact]
    public void ParseEmailList_NullInput_ReturnsNull()
    {
        var result = EmailSchedulerService.ParseEmailList(null!);
        Assert.Null(result);
    }

    [Fact]
    public void ParseEmailList_EmptyString_ReturnsNull()
    {
        var result = EmailSchedulerService.ParseEmailList("");
        Assert.Null(result);
    }

    [Fact]
    public void ParseEmailList_WhitespaceOnly_ReturnsNull()
    {
        var result = EmailSchedulerService.ParseEmailList("   ");
        Assert.Null(result);
    }

    [Fact]
    public void ParseEmailList_SingleValidEmail_ReturnsList()
    {
        var result = EmailSchedulerService.ParseEmailList("test@example.com");
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Contains("test@example.com", result);
    }

    [Fact]
    public void ParseEmailList_MultipleEmails_ReturnsAll()
    {
        var result = EmailSchedulerService.ParseEmailList("a@b.com, c@d.com, e@f.com");
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void ParseEmailList_InvalidEntries_FiltersOut()
    {
        var result = EmailSchedulerService.ParseEmailList("valid@example.com, not-an-email, another@test.com");
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain("not-an-email", result);
    }

    [Fact]
    public void ParseEmailList_AllInvalid_ReturnsNull()
    {
        var result = EmailSchedulerService.ParseEmailList("not-email, also-not-email");
        Assert.Null(result);
    }

    [Fact]
    public async Task SendCustomerEmailAsync_NullEmail_SkipsSend()
    {
        using var scope = _serviceProvider.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IEmailRenderer>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var customer = new Customer
        {
            Name = "Test",
            Email = "",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        await service.SendCustomerEmailAsync(customer, renderer, sender, CancellationToken.None);

        _mockSender.Verify(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessDueCustomers_UpdatesSystemInfoMetrics()
    {
        var pastTime = _fakeTime.GetUtcNow().UtcDateTime.AddMinutes(-5);
        await SeedCustomerAsync("test@example.com", nextSendTimeUtc: pastTime);

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var systemInfoMock = new Mock<ISystemInfoService>();
        var service = new EmailSchedulerService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmailSchedulerService>.Instance,
            Options.Create(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1", SendTimesUtc = new() { "08:00" } }),
            systemInfoMock.Object,
            _fakeTime);

        await service.ProcessDueCustomersAsync(CancellationToken.None);

        systemInfoMock.Verify(s => s.SetBatchAsync("Runtime",
            It.Is<Dictionary<string, string>>(d => d.ContainsKey("runtime.last_email_sent") && d.ContainsKey("runtime.total_emails_sent")),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessDueCustomers_SystemInfoFailure_DoesNotCrashService()
    {
        var pastTime = _fakeTime.GetUtcNow().UtcDateTime.AddMinutes(-5);
        await SeedCustomerAsync("test@example.com", nextSendTimeUtc: pastTime);

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var systemInfoMock = new Mock<ISystemInfoService>();
        systemInfoMock.Setup(s => s.SetBatchAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("System info write failed"));

        var service = new EmailSchedulerService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmailSchedulerService>.Instance,
            Options.Create(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1", SendTimesUtc = new() { "08:00" } }),
            systemInfoMock.Object,
            _fakeTime);

        // Should not throw
        var result = await service.ProcessDueCustomersAsync(CancellationToken.None);
        Assert.True(result);
    }

    [Fact]
    public async Task ProcessDueCustomers_EmptyCcBcc_PassesNullLists()
    {
        var pastTime = _fakeTime.GetUtcNow().UtcDateTime.AddMinutes(-5);
        await SeedCustomerAsync("main@example.com", CustomerStatus.Active, "", "", nextSendTimeUtc: pastTime);

        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1", SendTimesUtc = new() { "08:00" } });
        await service.ProcessDueCustomersAsync(CancellationToken.None);

        _mockSender.Verify(s => s.SendEmailAsync(
            It.IsAny<List<string>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            null,
            null,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InitializeNextSendTimes_SkipsCustomersWithExistingNextSendTime()
    {
        var existingTime = _fakeTime.GetUtcNow().UtcDateTime.AddHours(5);
        await SeedCustomerAsync("existing@example.com", nextSendTimeUtc: existingTime);

        var service = CreateService(new EmailSettings { SendTimesUtc = new() { "08:00" }, FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" });
        await service.InitializeNextSendTimesAsync(CancellationToken.None);

        using var db = CreateDb();
        var customer = await db.Customers.FirstAsync();
        // NextSendTimeUtc should remain unchanged
        Assert.Equal(existingTime, customer.NextSendTimeUtc);
    }

    [Fact]
    public async Task SendCustomerEmailAsync_PassesTimeProviderNowUtc_ToRenderer()
    {
        // Use a fixed time that is clearly distinguishable from DateTime.UtcNow
        var fixedTime = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var mockTimeProvider = new Mock<TimeProvider>();
        mockTimeProvider.Setup(tp => tp.GetUtcNow()).Returns(fixedTime);

        DateTime? capturedNowUtc = null;
        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, DateTime?, CancellationToken>((id, tz, nowUtc, ct) => capturedNowUtc = nowUtc)
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var scope = _serviceProvider.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IEmailRenderer>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var customer = new Customer
        {
            Name = "Test",
            Email = "test@example.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };

        var service = new EmailSchedulerService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmailSchedulerService>.Instance,
            Options.Create(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" }),
            new Mock<ISystemInfoService>().Object,
            mockTimeProvider.Object);

        await service.SendCustomerEmailAsync(customer, renderer, sender, CancellationToken.None);

        Assert.NotNull(capturedNowUtc);
        Assert.Equal(fixedTime.UtcDateTime, capturedNowUtc.Value);
    }

    [Fact]
    public async Task ProcessDueCustomers_PassesNowUtcToRenderer()
    {
        var pastTime = _fakeTime.GetUtcNow().UtcDateTime.AddMinutes(-5);
        await SeedCustomerAsync("test@example.com", nextSendTimeUtc: pastTime);

        DateTime? capturedNowUtc = null;
        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, DateTime?, CancellationToken>((id, tz, nowUtc, ct) => capturedNowUtc = nowUtc)
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1", SendTimesUtc = new() { "08:00" } });
        await service.ProcessDueCustomersAsync(CancellationToken.None);

        // Verify the renderer received a non-null nowUtc (proving the parameter is passed)
        Assert.NotNull(capturedNowUtc);
    }

    [Fact]
    public async Task SendCustomerEmailAsync_RendererAndSubjectUseConsistentTime()
    {
        // Use a fixed time so we can verify both the renderer and subject use the same time source
        var fixedTime = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var mockTimeProvider = new Mock<TimeProvider>();
        mockTimeProvider.Setup(tp => tp.GetUtcNow()).Returns(fixedTime);

        DateTime? capturedNowUtc = null;
        string? capturedSubject = null;
        _mockRenderer.Setup(r => r.RenderDailySummaryAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, DateTime?, CancellationToken>((id, tz, nowUtc, ct) => capturedNowUtc = nowUtc)
            .ReturnsAsync("<html>Summary</html>");
        _mockSender.Setup(s => s.SendEmailAsync(It.IsAny<List<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>?>(), It.IsAny<List<string>?>(), It.IsAny<CancellationToken>()))
            .Callback<List<string>, string, string, List<string>?, List<string>?, CancellationToken>((to, subj, html, cc, bcc, ct) => capturedSubject = subj)
            .Returns(Task.CompletedTask);

        using var scope = _serviceProvider.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IEmailRenderer>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var customer = new Customer
        {
            Name = "Test",
            Email = "test@example.com",
            TimeZoneId = "UTC",
            Status = CustomerStatus.Active
        };

        var service = new EmailSchedulerService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmailSchedulerService>.Instance,
            Options.Create(new EmailSettings { FromAddress = "noreply@hpoll.com", AwsRegion = "us-east-1" }),
            new Mock<ISystemInfoService>().Object,
            mockTimeProvider.Object);

        await service.SendCustomerEmailAsync(customer, renderer, sender, CancellationToken.None);

        // Both the renderer nowUtc and the subject date should be derived from the same TimeProvider
        Assert.NotNull(capturedNowUtc);
        Assert.Equal(fixedTime.UtcDateTime, capturedNowUtc.Value);
        Assert.NotNull(capturedSubject);
        Assert.Contains("15 Jun 2025", capturedSubject);
    }
}
