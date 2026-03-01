using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Moq;
using Hpoll.Admin.Pages;

namespace Hpoll.Admin.Tests;

public class LoginModelTests : IDisposable
{
    private static readonly PasswordHasher<object> Hasher = new();
    private readonly string _testHash;

    public LoginModelTests()
    {
        _testHash = Hasher.HashPassword(null!, "correctpassword");
    }

    public void Dispose()
    {
        // Clean up env var after each test
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD_HASH", null);
    }

    private LoginModel CreatePageModel(string? ipAddress = "127.0.0.1")
    {
        var httpContext = new DefaultHttpContext();
        if (ipAddress != null)
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);

        // Set up a mock auth service so SignInAsync doesn't throw
        var authService = new Mock<IAuthenticationService>();
        authService.Setup(a => a.SignInAsync(
            It.IsAny<HttpContext>(),
            It.IsAny<string>(),
            It.IsAny<System.Security.Claims.ClaimsPrincipal>(),
            It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(s => s.GetService(typeof(IAuthenticationService)))
            .Returns(authService.Object);

        httpContext.RequestServices = serviceProvider.Object;

        var model = new LoginModel();
        model.PageContext = new PageContext
        {
            ActionDescriptor = new CompiledPageActionDescriptor(),
            HttpContext = httpContext,
            RouteData = new RouteData()
        };
        return model;
    }

    [Fact]
    public void OnGet_NoPasswordHash_SetsSetupMode()
    {
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD_HASH", null);

        var model = CreatePageModel();
        model.OnGet();

        Assert.True(model.IsSetupMode);
    }

    [Fact]
    public void OnGet_WithPasswordHash_NotSetupMode()
    {
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD_HASH", _testHash);

        var model = CreatePageModel();
        model.OnGet();

        Assert.False(model.IsSetupMode);
    }

    [Fact]
    public async Task OnPostAsync_NoPasswordHashConfigured_ReturnsSetupMode()
    {
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD_HASH", null);

        var model = CreatePageModel();
        var result = await model.OnPostAsync("anypassword");

        Assert.IsType<PageResult>(result);
        Assert.True(model.IsSetupMode);
        Assert.Contains("not configured", model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_CorrectPassword_RedirectsToIndex()
    {
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD_HASH", _testHash);

        var model = CreatePageModel();
        var result = await model.OnPostAsync("correctpassword");

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Index", redirect.PageName);
    }

    [Fact]
    public async Task OnPostAsync_WrongPassword_ReturnsError()
    {
        Environment.SetEnvironmentVariable("ADMIN_PASSWORD_HASH", _testHash);

        var model = CreatePageModel();
        var result = await model.OnPostAsync("wrongpassword");

        Assert.IsType<PageResult>(result);
        Assert.Equal("Invalid password.", model.ErrorMessage);
    }

    [Fact]
    public void OnPostSetup_ValidPassword_ReturnsHash()
    {
        var model = CreatePageModel();
        var result = model.OnPostSetup("validpassword123", "validpassword123");

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.GeneratedHash);
        Assert.True(model.IsSetupMode);
    }

    [Fact]
    public void OnPostSetup_ShortPassword_ReturnsError()
    {
        var model = CreatePageModel();
        var result = model.OnPostSetup("short", "short");

        Assert.IsType<PageResult>(result);
        Assert.Contains("at least 8 characters", model.ErrorMessage);
        Assert.Null(model.GeneratedHash);
    }

    [Fact]
    public void OnPostSetup_MismatchedPasswords_ReturnsError()
    {
        var model = CreatePageModel();
        var result = model.OnPostSetup("validpassword123", "differentpassword");

        Assert.IsType<PageResult>(result);
        Assert.Contains("do not match", model.ErrorMessage);
    }

    [Fact]
    public void OnPostSetup_EmptyPassword_ReturnsError()
    {
        var model = CreatePageModel();
        var result = model.OnPostSetup("", "");

        Assert.IsType<PageResult>(result);
        Assert.Contains("required", model.ErrorMessage);
    }
}
