using System.Net;

namespace Hpoll.Admin.Tests.Integration;

public class LayoutAndNavigationTests : IClassFixture<HpollWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;

    public LayoutAndNavigationTests(HpollWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/About")]
    [InlineData("/Customers")]
    public async Task AuthenticatedPages_UseSharedLayout(string path)
    {
        var response = await _client.GetAsync(path);

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        // Shared layout elements
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("<nav>", html);
        Assert.Contains("class=\"brand\"", html);
        Assert.Contains("hpoll", html);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/About")]
    [InlineData("/Customers")]
    public async Task AuthenticatedPages_HaveNavigationLinks(string path)
    {
        var response = await _client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Dashboard", html);
        Assert.Contains("Customers", html);
        Assert.Contains("About", html);
        Assert.Contains("Logout", html);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/About")]
    [InlineData("/Customers")]
    public async Task AuthenticatedPages_HaveSecurityHeaders(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.True(response.Headers.Contains("X-Content-Type-Options")
            || response.Content.Headers.Contains("X-Content-Type-Options")
            || response.Headers.TryGetValues("X-Content-Type-Options", out _));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/About")]
    [InlineData("/Customers")]
    public async Task AuthenticatedPages_SetPageTitle(string path)
    {
        var response = await _client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<title>", html);
        Assert.Contains("hpoll admin - Testing</title>", html);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/About")]
    [InlineData("/Customers")]
    public async Task AuthenticatedPages_NavbarUsesEnvironmentColor(string path)
    {
        var response = await _client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        // Testing environment uses the default switch branch color (green)
        Assert.Contains("#1d3317", html);
    }

    [Fact]
    public async Task LogoutEndpoint_IsPostOnly()
    {
        var response = await _client.GetAsync("/Logout");

        // GET to /Logout should not be a valid endpoint
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose() => _client.Dispose();
}
