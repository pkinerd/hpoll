using System.Net;
using System.Text.RegularExpressions;

namespace Hpoll.Admin.Tests.Integration;

public class LayoutAndNavigationTests : IClassFixture<HpollWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly HpollWebApplicationFactory _factory;

    public LayoutAndNavigationTests(HpollWebApplicationFactory factory)
    {
        _factory = factory;
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

    [Fact]
    public async Task LogoutEndpoint_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.PostAsync("/Logout", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LogoutEndpoint_WithAntiforgeryToken_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // GET a page to obtain the antiforgery token and cookie
        var pageResponse = await client.GetAsync("/");
        pageResponse.EnsureSuccessStatusCode();
        var html = await pageResponse.Content.ReadAsStringAsync();

        // Extract the __RequestVerificationToken from the hidden input
        var tokenMatch = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
        Assert.True(tokenMatch.Success, "Antiforgery token not found in page HTML");
        var token = tokenMatch.Groups[1].Value;

        // POST to /Logout with the token
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        });
        var response = await client.PostAsync("/Logout", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task LogoutForm_ContainsAntiforgeryToken()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        // The logout form should contain the antiforgery token hidden field
        Assert.Matches(@"action=""/Logout"".*__RequestVerificationToken", html);
    }

    public void Dispose() => _client.Dispose();
}
