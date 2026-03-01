using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Hpoll.Admin.Pages.Hubs;
using Hpoll.Core.Interfaces;
using Hpoll.Core.Models;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Tests.Hubs;

public class DetailModelTests : IDisposable
{
    private readonly HpollDbContext _db;
    private readonly Mock<IHueApiClient> _mockHueClient;

    public DetailModelTests()
    {
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new HpollDbContext(options);
        _mockHueClient = new Mock<IHueApiClient>();
    }

    public void Dispose() => _db.Dispose();

    private DetailModel CreatePageModel()
    {
        var model = new DetailModel(_db, _mockHueClient.Object, NullLogger<DetailModel>.Instance);
        model.PageContext = new PageContext
        {
            ActionDescriptor = new CompiledPageActionDescriptor(),
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData()
        };
        return model;
    }

    private async Task<(Customer customer, Hub hub)> SeedDataAsync(string hubStatus = "active")
    {
        var customer = new Customer { Name = "Test User", Email = "test@example.com", TimeZoneId = "UTC" };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var hub = new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "001788FFFE123456",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = hubStatus
        };
        _db.Hubs.Add(hub);
        await _db.SaveChangesAsync();

        return (customer, hub);
    }

    [Fact]
    public async Task OnGetAsync_ValidHub_LoadsDetails()
    {
        var (_, hub) = await SeedDataAsync();

        var model = CreatePageModel();
        var result = await model.OnGetAsync(hub.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal(hub.Id, model.Hub.Id);
        Assert.Equal("001788FFFE123456", model.Hub.HueBridgeId);
    }

    [Fact]
    public async Task OnGetAsync_InvalidHub_ReturnsNotFound()
    {
        var model = CreatePageModel();
        var result = await model.OnGetAsync(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostToggleStatusAsync_ActiveToInactive_SetsDeactivatedAt()
    {
        var (_, hub) = await SeedDataAsync("active");

        var model = CreatePageModel();
        var result = await model.OnPostToggleStatusAsync(hub.Id);

        Assert.IsType<RedirectToPageResult>(result);

        var updated = await _db.Hubs.FindAsync(hub.Id);
        Assert.Equal("inactive", updated!.Status);
        Assert.NotNull(updated.DeactivatedAt);
    }

    [Fact]
    public async Task OnPostToggleStatusAsync_InactiveToActive_ClearsDeactivatedAt()
    {
        var (_, hub) = await SeedDataAsync("inactive");
        hub.DeactivatedAt = DateTime.UtcNow.AddDays(-2);
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        var result = await model.OnPostToggleStatusAsync(hub.Id);

        Assert.IsType<RedirectToPageResult>(result);

        var updated = await _db.Hubs.FindAsync(hub.Id);
        Assert.Equal("active", updated!.Status);
        Assert.Null(updated.DeactivatedAt);
    }

    [Fact]
    public async Task OnPostDeleteAsync_InactiveOver10Minutes_DeletesHub()
    {
        var (customer, hub) = await SeedDataAsync("inactive");
        hub.DeactivatedAt = DateTime.UtcNow.AddMinutes(-11);
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        var result = await model.OnPostDeleteAsync(hub.Id);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Customers/Detail", redirect.PageName);

        Assert.Null(await _db.Hubs.FindAsync(hub.Id));
    }

    [Fact]
    public async Task OnPostDeleteAsync_InactiveUnder10Minutes_DoesNotDelete()
    {
        var (_, hub) = await SeedDataAsync("inactive");
        hub.DeactivatedAt = DateTime.UtcNow.AddMinutes(-5);
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        var result = await model.OnPostDeleteAsync(hub.Id);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.NotNull(await _db.Hubs.FindAsync(hub.Id));
    }

    [Fact]
    public async Task OnPostDeleteAsync_ActiveHub_DoesNotDelete()
    {
        var (_, hub) = await SeedDataAsync("active");

        var model = CreatePageModel();
        var result = await model.OnPostDeleteAsync(hub.Id);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.NotNull(await _db.Hubs.FindAsync(hub.Id));
    }

    [Fact]
    public async Task OnPostDeleteAsync_InvalidHub_ReturnsNotFound()
    {
        var model = CreatePageModel();
        var result = await model.OnPostDeleteAsync(999);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostClearReauthAsync_ClearsNeedsReauth()
    {
        var (_, hub) = await SeedDataAsync("needs_reauth");
        hub.ConsecutiveFailures = 5;
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        var result = await model.OnPostClearReauthAsync(hub.Id);

        Assert.IsType<RedirectToPageResult>(result);

        var updated = await _db.Hubs.FindAsync(hub.Id);
        Assert.Equal("active", updated!.Status);
        Assert.Equal(0, updated.ConsecutiveFailures);
    }

    [Fact]
    public async Task OnPostClearReauthAsync_NotNeedsReauth_DoesNothing()
    {
        var (_, hub) = await SeedDataAsync("active");

        var model = CreatePageModel();
        var result = await model.OnPostClearReauthAsync(hub.Id);

        Assert.IsType<RedirectToPageResult>(result);

        var updated = await _db.Hubs.FindAsync(hub.Id);
        Assert.Equal("active", updated!.Status);
    }

    [Fact]
    public async Task OnPostRefreshTokenAsync_Success_UpdatesTokens()
    {
        var (_, hub) = await SeedDataAsync();

        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueTokenResponse
            {
                AccessToken = "new-access",
                RefreshToken = "new-refresh",
                TokenType = "bearer",
                ExpiresIn = 604800
            });

        var model = CreatePageModel();
        var result = await model.OnPostRefreshTokenAsync(hub.Id);

        Assert.IsType<PageResult>(result);
        Assert.Contains("Token refreshed", model.SuccessMessage);

        var updated = await _db.Hubs.FindAsync(hub.Id);
        Assert.Equal("new-access", updated!.AccessToken);
        Assert.Equal("new-refresh", updated.RefreshToken);
    }

    [Fact]
    public async Task OnPostRefreshTokenAsync_HttpError_ShowsErrorMessage()
    {
        var (_, hub) = await SeedDataAsync();

        _mockHueClient.Setup(c => c.RefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unauthorized", null, System.Net.HttpStatusCode.Unauthorized));

        var model = CreatePageModel();
        var result = await model.OnPostRefreshTokenAsync(hub.Id);

        Assert.IsType<PageResult>(result);
        Assert.Contains("HTTP 401", model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostTestConnectionAsync_Success_ShowsDeviceCount()
    {
        var (_, hub) = await SeedDataAsync();

        _mockHueClient.Setup(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDeviceResource>
            {
                Data = new List<HueDeviceResource>
                {
                    new() { Id = "dev1", Metadata = new HueDeviceMetadata { Name = "Sensor 1" } },
                    new() { Id = "dev2", Metadata = new HueDeviceMetadata { Name = "Sensor 2" } }
                }
            });

        var model = CreatePageModel();
        var result = await model.OnPostTestConnectionAsync(hub.Id);

        Assert.IsType<PageResult>(result);
        Assert.Contains("2 device(s)", model.SuccessMessage);
    }

    [Fact]
    public async Task OnPostTestConnectionAsync_Failure_ShowsError()
    {
        var (_, hub) = await SeedDataAsync();

        _mockHueClient.Setup(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable));

        var model = CreatePageModel();
        var result = await model.OnPostTestConnectionAsync(hub.Id);

        Assert.IsType<PageResult>(result);
        Assert.Contains("HTTP 503", model.ErrorMessage);
    }

    [Theory]
    [InlineData("appkey", "key")]
    [InlineData("access", "token")]
    [InlineData("refresh", "refresh")]
    public async Task OnGetTokenAsync_ValidType_ReturnsTokenValue(string type, string expected)
    {
        var (_, hub) = await SeedDataAsync();

        var model = CreatePageModel();
        var result = await model.OnGetTokenAsync(hub.Id, type);

        var json = Assert.IsType<JsonResult>(result);
        var value = json.Value!.GetType().GetProperty("value")!.GetValue(json.Value) as string;
        Assert.Equal(expected, value);
    }

    [Fact]
    public async Task OnGetTokenAsync_InvalidHub_ReturnsNotFound()
    {
        var model = CreatePageModel();
        var result = await model.OnGetTokenAsync(999, "access");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetTokenAsync_InvalidType_ReturnsBadRequest()
    {
        var (_, hub) = await SeedDataAsync();

        var model = CreatePageModel();
        var result = await model.OnGetTokenAsync(hub.Id, "invalid");

        Assert.IsType<BadRequestResult>(result);
    }
}
