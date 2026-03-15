using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Hpoll.Admin.Services;
using Hpoll.Core.Configuration;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Tests.Services;

public class SendTimeDisplayServiceTests : IDisposable
{
    private readonly HpollDbContext _db;
    private readonly EmailSettings _emailSettings;

    public SendTimeDisplayServiceTests()
    {
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new HpollDbContext(options);
        _emailSettings = new EmailSettings { SendTimesUtc = new List<string> { "08:00", "20:00" } };
    }

    public void Dispose() => _db.Dispose();

    private SendTimeDisplayService CreateService() =>
        new(_db, Options.Create(_emailSettings));

    [Fact]
    public async Task GetEffectiveDefaultSendTimesUtcAsync_DbEntryWithValidTimes_ReturnsParsedTimes()
    {
        _db.SystemInfo.Add(new SystemInfo { Key = "email.send_times_utc", Value = "09:30,14:00,21:15" });
        await _db.SaveChangesAsync();

        var service = CreateService();
        var result = await service.GetEffectiveDefaultSendTimesUtcAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal("09:30", result[0]);
        Assert.Equal("14:00", result[1]);
        Assert.Equal("21:15", result[2]);
    }

    [Fact]
    public async Task GetEffectiveDefaultSendTimesUtcAsync_DbEntryWithInvalidValues_FallsBackToConfig()
    {
        _db.SystemInfo.Add(new SystemInfo { Key = "email.send_times_utc", Value = "not-a-time,also-bad" });
        await _db.SaveChangesAsync();

        var service = CreateService();
        var result = await service.GetEffectiveDefaultSendTimesUtcAsync();

        Assert.Equal(_emailSettings.SendTimesUtc, result);
    }

    [Fact]
    public async Task GetEffectiveDefaultSendTimesUtcAsync_DbEntryEmpty_FallsBackToConfig()
    {
        _db.SystemInfo.Add(new SystemInfo { Key = "email.send_times_utc", Value = "" });
        await _db.SaveChangesAsync();

        var service = CreateService();
        var result = await service.GetEffectiveDefaultSendTimesUtcAsync();

        Assert.Equal(_emailSettings.SendTimesUtc, result);
    }

    [Fact]
    public async Task GetEffectiveDefaultSendTimesUtcAsync_NoDbEntry_FallsBackToConfig()
    {
        var service = CreateService();
        var result = await service.GetEffectiveDefaultSendTimesUtcAsync();

        Assert.Equal(_emailSettings.SendTimesUtc, result);
    }

    [Fact]
    public async Task GetEffectiveDefaultSendTimesUtcAsync_MixedValidAndInvalid_ReturnsOnlyValid()
    {
        _db.SystemInfo.Add(new SystemInfo { Key = "email.send_times_utc", Value = "08:00,invalid,16:30" });
        await _db.SaveChangesAsync();

        var service = CreateService();
        var result = await service.GetEffectiveDefaultSendTimesUtcAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("08:00", result[0]);
        Assert.Equal("16:30", result[1]);
    }

    [Fact]
    public async Task GetDefaultSendTimesDisplayAsync_FormatsWithUtcSuffix()
    {
        _db.SystemInfo.Add(new SystemInfo { Key = "email.send_times_utc", Value = "08:45" });
        await _db.SaveChangesAsync();

        var service = CreateService();
        var result = await service.GetDefaultSendTimesDisplayAsync();

        Assert.Equal("08:45 UTC", result);
    }

    [Fact]
    public async Task GetDefaultSendTimesDisplayAsync_MultipleTimes_CommaSeparated()
    {
        _db.SystemInfo.Add(new SystemInfo { Key = "email.send_times_utc", Value = "08:00,20:00" });
        await _db.SaveChangesAsync();

        var service = CreateService();
        var result = await service.GetDefaultSendTimesDisplayAsync();

        Assert.Equal("08:00, 20:00 UTC", result);
    }

    [Fact]
    public async Task GetEffectiveDefaultSendTimesUtcAsync_WhitespaceOnly_FallsBackToConfig()
    {
        _db.SystemInfo.Add(new SystemInfo { Key = "email.send_times_utc", Value = "   " });
        await _db.SaveChangesAsync();

        var service = CreateService();
        var result = await service.GetEffectiveDefaultSendTimesUtcAsync();

        Assert.Equal(_emailSettings.SendTimesUtc, result);
    }
}
