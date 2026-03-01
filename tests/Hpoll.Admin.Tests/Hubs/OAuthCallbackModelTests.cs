using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Hpoll.Admin.Pages.Hubs;
using Hpoll.Core.Constants;
using Hpoll.Core.Interfaces;
using Hpoll.Core.Models;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Tests.Hubs;

public class OAuthCallbackModelTests : IDisposable
{
    private readonly HpollDbContext _db;
    private readonly Mock<IHueApiClient> _mockHueClient;

    public OAuthCallbackModelTests()
    {
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new HpollDbContext(options);
        _mockHueClient = new Mock<IHueApiClient>();
    }

    public void Dispose() => _db.Dispose();

    private OAuthCallbackModel CreatePageModel(int? oauthCustomerId = null, string? oauthCsrf = null)
    {
        var model = new OAuthCallbackModel(_db, _mockHueClient.Object, NullLogger<OAuthCallbackModel>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");

        // Set up session with a simple mock
        var session = new TestSession();
        if (oauthCustomerId.HasValue)
            session.SetInt32("OAuthCustomerId", oauthCustomerId.Value);
        if (oauthCsrf != null)
            session.SetString("OAuthCsrf", oauthCsrf);
        httpContext.Session = session;

        model.PageContext = new PageContext
        {
            ActionDescriptor = new CompiledPageActionDescriptor(),
            HttpContext = httpContext,
            RouteData = new RouteData()
        };
        return model;
    }

    private async Task<Customer> SeedCustomerAsync(string name = "Test User", string email = "test@example.com")
    {
        var customer = new Customer { Name = name, Email = email, TimeZoneId = "UTC", Status = CustomerStatus.Active };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return customer;
    }

    [Fact]
    public async Task OnGetAsync_WithError_ReturnsPageWithErrorMessage()
    {
        var model = CreatePageModel();

        var result = await model.OnGetAsync(null, null, "access_denied");

        Assert.IsType<PageResult>(result);
        Assert.False(model.Success);
        Assert.Contains("denied", model.Message);
    }

    [Fact]
    public async Task OnGetAsync_MissingCode_ReturnsPageWithErrorMessage()
    {
        var model = CreatePageModel();

        var result = await model.OnGetAsync(null, "1:csrf", null);

        Assert.IsType<PageResult>(result);
        Assert.False(model.Success);
        Assert.Contains("Missing", model.Message);
    }

    [Fact]
    public async Task OnGetAsync_MissingState_ReturnsPageWithErrorMessage()
    {
        var model = CreatePageModel();

        var result = await model.OnGetAsync("auth-code", null, null);

        Assert.IsType<PageResult>(result);
        Assert.False(model.Success);
        Assert.Contains("Missing", model.Message);
    }

    [Fact]
    public async Task OnGetAsync_InvalidStateFormat_ReturnsPageWithErrorMessage()
    {
        var model = CreatePageModel();

        var result = await model.OnGetAsync("auth-code", "invalid-state", null);

        Assert.IsType<PageResult>(result);
        Assert.False(model.Success);
        Assert.Contains("Invalid state", model.Message);
    }

    [Fact]
    public async Task OnGetAsync_StateWithNonIntCustomerId_ReturnsError()
    {
        var model = CreatePageModel();

        var result = await model.OnGetAsync("auth-code", "notanint:csrf-token", null);

        Assert.IsType<PageResult>(result);
        Assert.Contains("Invalid state", model.Message);
    }

    [Fact]
    public async Task OnGetAsync_CsrfMismatch_ReturnsPageWithCsrfError()
    {
        var customer = await SeedCustomerAsync();
        var model = CreatePageModel(oauthCustomerId: customer.Id, oauthCsrf: "expected-csrf");

        var result = await model.OnGetAsync("auth-code", $"{customer.Id}:wrong-csrf", null);

        Assert.IsType<PageResult>(result);
        Assert.False(model.Success);
        Assert.Contains("CSRF", model.Message);
    }

    [Fact]
    public async Task OnGetAsync_CustomerIdMismatch_ReturnsPageWithCsrfError()
    {
        var customer = await SeedCustomerAsync();
        var model = CreatePageModel(oauthCustomerId: 999, oauthCsrf: "csrf-token");

        var result = await model.OnGetAsync("auth-code", $"{customer.Id}:csrf-token", null);

        Assert.IsType<PageResult>(result);
        Assert.False(model.Success);
        Assert.Contains("CSRF", model.Message);
    }

    [Fact]
    public async Task OnGetAsync_NoCsrfInSession_ReturnsPageWithCsrfError()
    {
        var customer = await SeedCustomerAsync();
        var model = CreatePageModel(oauthCustomerId: customer.Id, oauthCsrf: null);

        var result = await model.OnGetAsync("auth-code", $"{customer.Id}:some-csrf", null);

        Assert.IsType<PageResult>(result);
        Assert.Contains("CSRF", model.Message);
    }

    [Fact]
    public async Task OnGetAsync_CustomerNotFound_ReturnsPageWithError()
    {
        var model = CreatePageModel(oauthCustomerId: 999, oauthCsrf: "csrf-token");

        var result = await model.OnGetAsync("auth-code", "999:csrf-token", null);

        Assert.IsType<PageResult>(result);
        Assert.Contains("Customer not found", model.Message);
    }

    [Fact]
    public async Task OnGetAsync_SuccessfulNewHubRegistration_CreatesHub()
    {
        var customer = await SeedCustomerAsync();
        var csrfToken = "valid-csrf";
        var model = CreatePageModel(oauthCustomerId: customer.Id, oauthCsrf: csrfToken);

        _mockHueClient.Setup(c => c.ExchangeAuthorizationCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueTokenResponse { AccessToken = "access-tk", RefreshToken = "refresh-tk", ExpiresIn = 604800 });
        _mockHueClient.Setup(c => c.EnableLinkButtonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockHueClient.Setup(c => c.RegisterApplicationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("app-key-123");
        _mockHueClient.Setup(c => c.GetBridgeIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("001788FFFE123456");
        _mockHueClient.Setup(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueResponse<HueDeviceResource>
            {
                Data = new List<HueDeviceResource>
                {
                    new() { Id = "dev-1", Type = "device", Metadata = new HueDeviceMetadata { Name = "Sensor" }, ProductData = new HueProductData { ModelId = "SML001", ProductName = "Hue", SoftwareVersion = "1.0" }, Services = new List<HueResourceRef>() }
                }
            });

        var result = await model.OnGetAsync("auth-code", $"{customer.Id}:{csrfToken}", null);

        Assert.IsType<PageResult>(result);
        Assert.True(model.Success);
        Assert.Equal("001788FFFE123456", model.BridgeId);
        Assert.Equal(1, model.DeviceCount);
        Assert.Equal(customer.Id, model.CustomerId);

        var hub = await _db.Hubs.FirstOrDefaultAsync(h => h.HueBridgeId == "001788FFFE123456");
        Assert.NotNull(hub);
        Assert.Equal(HubStatus.Active, hub.Status);
        Assert.Equal("access-tk", hub.AccessToken);
        Assert.Equal("app-key-123", hub.HueApplicationKey);
    }

    [Fact]
    public async Task OnGetAsync_ExistingHub_UpdatesTokens()
    {
        var customer = await SeedCustomerAsync();
        var csrfToken = "valid-csrf";

        // Pre-existing hub with old tokens
        _db.Hubs.Add(new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "001788FFFE123456",
            HueApplicationKey = "old-app-key",
            AccessToken = "old-access",
            RefreshToken = "old-refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(-1),
            Status = HubStatus.NeedsReauth,
            ConsecutiveFailures = 5
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel(oauthCustomerId: customer.Id, oauthCsrf: csrfToken);

        _mockHueClient.Setup(c => c.ExchangeAuthorizationCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueTokenResponse { AccessToken = "new-access", RefreshToken = "new-refresh", ExpiresIn = 604800 });
        _mockHueClient.Setup(c => c.EnableLinkButtonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockHueClient.Setup(c => c.RegisterApplicationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-app-key");
        _mockHueClient.Setup(c => c.GetBridgeIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("001788FFFE123456");

        var result = await model.OnGetAsync("auth-code", $"{customer.Id}:{csrfToken}", null);

        Assert.IsType<PageResult>(result);
        Assert.True(model.Success);
        Assert.Contains("already existed", model.Message);

        var hub = await _db.Hubs.FirstAsync(h => h.HueBridgeId == "001788FFFE123456");
        Assert.Equal("new-access", hub.AccessToken);
        Assert.Equal("new-refresh", hub.RefreshToken);
        Assert.Equal(HubStatus.Active, hub.Status);
        Assert.Equal(0, hub.ConsecutiveFailures);
    }

    [Fact]
    public async Task OnGetAsync_HttpRequestException_ReturnsHttpStatusMessage()
    {
        var customer = await SeedCustomerAsync();
        var csrfToken = "valid-csrf";
        var model = CreatePageModel(oauthCustomerId: customer.Id, oauthCsrf: csrfToken);

        _mockHueClient.Setup(c => c.ExchangeAuthorizationCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Bad Request", null, System.Net.HttpStatusCode.BadRequest));

        var result = await model.OnGetAsync("auth-code", $"{customer.Id}:{csrfToken}", null);

        Assert.IsType<PageResult>(result);
        Assert.False(model.Success);
        Assert.Contains("HTTP 400", model.Message);
    }

    [Fact]
    public async Task OnGetAsync_HttpRequestException_NoStatusCode_ReturnsConnectivityMessage()
    {
        var customer = await SeedCustomerAsync();
        var csrfToken = "valid-csrf";
        var model = CreatePageModel(oauthCustomerId: customer.Id, oauthCsrf: csrfToken);

        _mockHueClient.Setup(c => c.ExchangeAuthorizationCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var result = await model.OnGetAsync("auth-code", $"{customer.Id}:{csrfToken}", null);

        Assert.IsType<PageResult>(result);
        Assert.False(model.Success);
        Assert.Contains("could not reach", model.Message);
    }

    [Fact]
    public async Task OnGetAsync_UnexpectedException_ReturnsGenericErrorMessage()
    {
        var customer = await SeedCustomerAsync();
        var csrfToken = "valid-csrf";
        var model = CreatePageModel(oauthCustomerId: customer.Id, oauthCsrf: csrfToken);

        _mockHueClient.Setup(c => c.ExchangeAuthorizationCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        var result = await model.OnGetAsync("auth-code", $"{customer.Id}:{csrfToken}", null);

        Assert.IsType<PageResult>(result);
        Assert.False(model.Success);
        Assert.Contains("unexpected error", model.Message);
    }

    [Fact]
    public async Task OnGetAsync_DeviceCountFetchFailure_StillSucceeds()
    {
        var customer = await SeedCustomerAsync();
        var csrfToken = "valid-csrf";
        var model = CreatePageModel(oauthCustomerId: customer.Id, oauthCsrf: csrfToken);

        _mockHueClient.Setup(c => c.ExchangeAuthorizationCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HueTokenResponse { AccessToken = "access-tk", RefreshToken = "refresh-tk", ExpiresIn = 604800 });
        _mockHueClient.Setup(c => c.EnableLinkButtonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockHueClient.Setup(c => c.RegisterApplicationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("app-key");
        _mockHueClient.Setup(c => c.GetBridgeIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("001788FFFE999999");
        _mockHueClient.Setup(c => c.GetDevicesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Timeout"));

        var result = await model.OnGetAsync("auth-code", $"{customer.Id}:{csrfToken}", null);

        Assert.IsType<PageResult>(result);
        Assert.True(model.Success);
        Assert.Null(model.DeviceCount);

        var hub = await _db.Hubs.FirstOrDefaultAsync(h => h.HueBridgeId == "001788FFFE999999");
        Assert.NotNull(hub);
    }

    /// <summary>
    /// Simple in-memory ISession implementation for testing.
    /// </summary>
    private class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public string Id => Guid.NewGuid().ToString();
        public bool IsAvailable => true;
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, out byte[] value)
        {
            if (_store.TryGetValue(key, out var stored))
            {
                value = stored;
                return true;
            }
            value = Array.Empty<byte>();
            return false;
        }
    }
}
