using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Moq;
using Hpoll.Admin.Pages;
using Hpoll.Core.Configuration;

namespace Hpoll.Admin.Tests;

public class LoginModelTests
{
    private static readonly PasswordHasher<object> Hasher = new();
    private readonly string _testHash;

    public LoginModelTests()
    {
        _testHash = Hasher.HashPassword(null!, "correctpassword");
    }

    private LoginModel CreatePageModel(string? passwordHash = null, string? ipAddress = "127.0.0.1")
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

        var settings = Options.Create(new AdminSettings { PasswordHash = passwordHash });
        var model = new LoginModel(settings);
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
        var model = CreatePageModel(passwordHash: null);
        model.OnGet();

        Assert.True(model.IsSetupMode);
    }

    [Fact]
    public void OnGet_WithPasswordHash_NotSetupMode()
    {
        var model = CreatePageModel(passwordHash: _testHash);
        model.OnGet();

        Assert.False(model.IsSetupMode);
    }

    [Fact]
    public async Task OnPostAsync_NoPasswordHashConfigured_ReturnsSetupMode()
    {
        var model = CreatePageModel(passwordHash: null);
        var result = await model.OnPostAsync("anypassword");

        Assert.IsType<PageResult>(result);
        Assert.True(model.IsSetupMode);
        Assert.Contains("not configured", model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_CorrectPassword_RedirectsToIndex()
    {
        var model = CreatePageModel(passwordHash: _testHash);
        var result = await model.OnPostAsync("correctpassword");

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Index", redirect.PageName);
    }

    [Fact]
    public async Task OnPostAsync_WrongPassword_ReturnsError()
    {
        var model = CreatePageModel(passwordHash: _testHash);
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

    [Fact]
    public async Task OnPostAsync_LockoutAfterMaxFailedAttempts()
    {
        // Use a unique IP to avoid cross-test interference (static dictionary)
        var ip = "10.0.0.1";

        // Submit 5 wrong passwords
        for (int i = 0; i < 5; i++)
        {
            var model = CreatePageModel(passwordHash: _testHash, ipAddress: ip);
            await model.OnPostAsync("wrongpassword");
        }

        // 6th attempt should be locked out
        var lockedModel = CreatePageModel(passwordHash: _testHash, ipAddress: ip);
        var result = await lockedModel.OnPostAsync("wrongpassword");

        Assert.IsType<PageResult>(result);
        Assert.Equal("Too many failed attempts. Please try again later.", lockedModel.ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_LockoutDoesNotAffectOtherIps()
    {
        var lockedIp = "10.0.0.2";
        var freeIp = "10.0.0.3";

        // Lock out one IP
        for (int i = 0; i < 5; i++)
        {
            var model = CreatePageModel(passwordHash: _testHash, ipAddress: lockedIp);
            await model.OnPostAsync("wrongpassword");
        }

        // Another IP should still be able to attempt login
        var freeModel = CreatePageModel(passwordHash: _testHash, ipAddress: freeIp);
        var result = await freeModel.OnPostAsync("wrongpassword");

        Assert.IsType<PageResult>(result);
        Assert.Equal("Invalid password.", freeModel.ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_CorrectPasswordWithReturnUrl_RedirectsToReturnUrl()
    {
        var model = CreatePageModel(passwordHash: _testHash, ipAddress: "10.0.0.4");
        model.PageContext.HttpContext.Request.QueryString = new QueryString("?ReturnUrl=/Customers");

        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(u => u.IsLocalUrl("/Customers")).Returns(true);
        model.Url = urlHelper.Object;

        var result = await model.OnPostAsync("correctpassword");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Customers", redirect.Url);
    }

    [Fact]
    public async Task OnPostAsync_CorrectPasswordClearsLockoutRecord()
    {
        var ip = "10.0.0.5";

        // Submit 4 wrong passwords (just under lockout threshold)
        for (int i = 0; i < 4; i++)
        {
            var model = CreatePageModel(passwordHash: _testHash, ipAddress: ip);
            await model.OnPostAsync("wrongpassword");
        }

        // Successful login clears the failed attempts
        var successModel = CreatePageModel(passwordHash: _testHash, ipAddress: ip);
        await successModel.OnPostAsync("correctpassword");

        // Should be able to fail again without hitting lockout
        var afterModel = CreatePageModel(passwordHash: _testHash, ipAddress: ip);
        var result = await afterModel.OnPostAsync("wrongpassword");

        Assert.IsType<PageResult>(result);
        Assert.Equal("Invalid password.", afterModel.ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_NullRemoteIpAddress_UsesUnknownFallback()
    {
        var model = CreatePageModel(passwordHash: _testHash, ipAddress: null);
        var result = await model.OnPostAsync("wrongpassword");

        Assert.IsType<PageResult>(result);
        Assert.Equal("Invalid password.", model.ErrorMessage);
    }

    [Fact]
    public void OnPostSetup_WhenPasswordAlreadyConfigured_ReturnsNotFound()
    {
        var model = CreatePageModel(passwordHash: _testHash);
        var result = model.OnPostSetup("newpassword123", "newpassword123");

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(model.GeneratedHash);
    }

    [Fact]
    public void OnPostSetup_WhenPasswordNotConfigured_AllowsHashGeneration()
    {
        var model = CreatePageModel(passwordHash: null);
        var result = model.OnPostSetup("validpassword123", "validpassword123");

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.GeneratedHash);
    }
}
