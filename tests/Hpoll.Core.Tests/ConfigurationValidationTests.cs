using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Services;
using Hpoll.Data;
using Hpoll.Email;

namespace Hpoll.Core.Tests;

public class ConfigurationValidationTests : IDisposable
{
    private readonly HpollDbContext _db;

    public ConfigurationValidationTests()
    {
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new HpollDbContext(options);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task EmailRenderer_ZeroSummaryWindowHours_ThrowsDivideByZero()
    {
        var emailSettings = Options.Create(new EmailSettings { SummaryWindowHours = 0 });
        var renderer = new EmailRenderer(_db, NullLogger<EmailRenderer>.Instance, emailSettings);

        await Assert.ThrowsAsync<DivideByZeroException>(
            () => renderer.RenderDailySummaryAsync(1, "UTC", DateTime.UtcNow));
    }

    [Fact]
    public async Task EmailRenderer_ZeroSummaryWindowCount_ProducesValidHtmlWithNoWindows()
    {
        var emailSettings = Options.Create(new EmailSettings { SummaryWindowCount = 0 });
        var renderer = new EmailRenderer(_db, NullLogger<EmailRenderer>.Instance, emailSettings);

        var html = await renderer.RenderDailySummaryAsync(1, "UTC", DateTime.UtcNow);

        Assert.NotNull(html);
        Assert.Contains("Daily Activity Summary", html);
        Assert.Contains("Motion Activity", html);
    }

    [Fact]
    public void PollingSettings_Defaults_AreReasonable()
    {
        var settings = new PollingSettings();

        Assert.Equal(60, settings.IntervalMinutes);
        Assert.Equal(168, settings.DataRetentionHours);
        Assert.Equal(30, settings.HttpTimeoutSeconds);
        Assert.Equal(3, settings.TokenRefreshMaxRetries);
        Assert.True(settings.IntervalMinutes > 0, "IntervalMinutes must be positive");
        Assert.True(settings.DataRetentionHours > 0, "DataRetentionHours must be positive");
    }

    [Fact]
    public void PollingSettings_DataRetentionExceedsBatteryPollInterval()
    {
        var settings = new PollingSettings();

        Assert.True(settings.DataRetentionHours >= settings.BatteryPollIntervalHours,
            "DataRetentionHours must be >= BatteryPollIntervalHours, otherwise battery readings are purged before the next poll");
    }

    [Fact]
    public void EmailSettings_Defaults_AreReasonable()
    {
        var settings = new EmailSettings();

        Assert.Single(settings.SendTimesUtc);
        Assert.Equal("08:00", settings.SendTimesUtc[0]);
        Assert.Equal(4, settings.SummaryWindowHours);
        Assert.Equal(7, settings.SummaryWindowCount);
        Assert.Equal(30, settings.BatteryAlertThreshold);
        Assert.True(settings.BatteryLevelCritical <= settings.BatteryLevelWarning,
            "Critical threshold should be <= warning threshold");
    }

    [Fact]
    public async Task EmailRenderer_NegativeSummaryWindowCount_ProducesValidHtml()
    {
        var emailSettings = Options.Create(new EmailSettings { SummaryWindowCount = -1 });
        var renderer = new EmailRenderer(_db, NullLogger<EmailRenderer>.Instance, emailSettings);

        var html = await renderer.RenderDailySummaryAsync(1, "UTC", DateTime.UtcNow);

        Assert.NotNull(html);
        Assert.Contains("Daily Activity Summary", html);
    }
}
