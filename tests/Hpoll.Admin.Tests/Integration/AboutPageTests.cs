using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Tests.Integration;

public class AboutPageTests : IClassFixture<HpollWebApplicationFactory>, IDisposable
{
    private readonly HpollWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AboutPageTests(HpollWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task About_ReturnsSuccessAndContainsTitle()
    {
        var response = await _client.GetAsync("/About");

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("About hpoll", html);
    }

    [Fact]
    public async Task About_ShowsEntityCountCards()
    {
        var response = await _client.GetAsync("/About");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Customers", html);
        Assert.Contains("Hubs", html);
        Assert.Contains("Devices", html);
    }

    [Fact]
    public async Task About_ShowsSystemInfoSections()
    {
        using var db = _factory.CreateDbContext();
        db.SystemInfo.Add(new SystemInfo
        {
            Key = "system.version",
            Value = "1.0.0-test",
            Category = "System"
        });
        db.SystemInfo.Add(new SystemInfo
        {
            Key = "polling.interval_minutes",
            Value = "60",
            Category = "Polling"
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/About");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("System", html);
        Assert.Contains("1.0.0-test", html);
        Assert.Contains("Polling", html);
        Assert.Contains("Interval Minutes", html);
    }

    [Fact]
    public async Task About_ShowsNoSystemInfoMessage_WhenWorkerNotStarted()
    {
        // With an empty SystemInfo table, should show a message
        using var db = _factory.CreateDbContext();
        // Remove all SystemInfo entries
        db.SystemInfo.RemoveRange(db.SystemInfo);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/About");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("No system information available", html);
    }

    [Fact]
    public async Task About_HasLayoutWithNavigation()
    {
        var response = await _client.GetAsync("/About");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<nav>", html);
        Assert.Contains("Dashboard", html);
        Assert.Contains("Customers", html);
    }

    public void Dispose() => _client.Dispose();
}
