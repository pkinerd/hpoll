using System.Net;
using Hpoll.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hpoll.Admin.Tests.Integration;

public class LoginPageTests : IClassFixture<HpollWebApplicationFactory>, IDisposable
{
    private readonly HpollWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LoginPageTests(HpollWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateAnonymousClient();
    }

    [Fact]
    public async Task Login_ReturnsSuccessStatusCode()
    {
        var response = await _client.GetAsync("/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_ContainsHpollBranding()
    {
        var response = await _client.GetAsync("/Login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("hpoll admin", html);
    }

    [Fact]
    public async Task Login_ContainsPasswordForm()
    {
        var response = await _client.GetAsync("/Login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<form", html);
        Assert.Contains("type=\"password\"", html);
        Assert.Contains("name=\"password\"", html);
    }

    [Fact]
    public async Task Login_HasNoLayoutNavigation()
    {
        var response = await _client.GetAsync("/Login");
        var html = await response.Content.ReadAsStringAsync();

        // Login uses Layout = null, so should NOT contain the nav bar
        Assert.DoesNotContain("<nav>", html);
        Assert.DoesNotContain("Dashboard</a>", html);
    }

    [Fact]
    public async Task Login_ShowsEitherSignInOrSetup()
    {
        var response = await _client.GetAsync("/Login");
        var html = await response.Content.ReadAsStringAsync();

        // Depending on env var state, shows either login or setup
        var hasSignIn = html.Contains("Sign in to continue");
        var hasSetup = html.Contains("Create a password");
        Assert.True(hasSignIn || hasSetup,
            "Login page should show either sign-in form or setup form");
    }

    [Fact]
    public async Task Login_HeaderUsesEnvironmentColor()
    {
        var response = await _client.GetAsync("/Login");
        var html = await response.Content.ReadAsStringAsync();

        // Testing environment uses the default switch branch color (green)
        Assert.Contains("login-header", html);
        Assert.Contains("#1d3317", html);
    }

    [Fact]
    public async Task Login_TitleIncludesEnvironmentName()
    {
        var response = await _client.GetAsync("/Login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("- Testing</title>", html);
    }

    [Fact]
    public async Task Login_SetupMode_ShowsPasswordSetupForm()
    {
        // Explicitly configure null password hash to ensure setup mode
        using var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<AdminSettings>(opts => opts.PasswordHash = null);
            });
        }).CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Login");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Create a password to secure the admin panel", html);
        Assert.Contains("hpoll admin setup", html);
        Assert.Contains("name=\"password\"", html);
        Assert.Contains("name=\"confirmPassword\"", html);
        Assert.Contains("Generate hash", html);
    }

    public void Dispose() => _client.Dispose();
}
