using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Hpoll.Core.Interfaces;
using Hpoll.Data;

namespace Hpoll.Admin.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory that replaces the real database with an in-memory
/// SQLite instance and bypasses authentication for integration tests.
/// </summary>
public class HpollWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    /// <summary>
    /// Mock IHueApiClient available for test setup of return values.
    /// </summary>
    public Mock<IHueApiClient> MockHueApiClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the real DbContext registration
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<HpollDbContext>));
            if (dbDescriptor != null) services.Remove(dbDescriptor);

            // Remove the real IHueApiClient registration
            var hueDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IHueApiClient));
            if (hueDescriptor != null) services.Remove(hueDescriptor);

            // Open a persistent in-memory SQLite connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<HpollDbContext>(options =>
                options.UseSqlite(_connection));

            // Register mock IHueApiClient
            services.AddScoped(_ => MockHueApiClient.Object);

            // Replace cookie auth with a test scheme that auto-authenticates
            services.AddAuthentication("TestScheme")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    "TestScheme", _ => { });
        });

        builder.ConfigureServices(services =>
        {
            // Ensure database is created with schema
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();
            db.Database.EnsureCreated();
        });
    }

    /// <summary>
    /// Creates an HttpClient that does NOT auto-authenticate (for testing anonymous pages like Login).
    /// </summary>
    public HttpClient CreateAnonymousClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        return client;
    }

    /// <summary>
    /// Gets a scoped DbContext for seeding data in tests.
    /// </summary>
    public HpollDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<HpollDbContext>();
    }

    /// <summary>
    /// Clears all data from every table, restoring the database to a clean
    /// schema-only state. Call this between tests to prevent intra-class
    /// data leakage.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HpollDbContext>();
        db.PollingLogs.RemoveRange(db.PollingLogs);
        db.DeviceReadings.RemoveRange(db.DeviceReadings);
        db.Devices.RemoveRange(db.Devices);
        db.Hubs.RemoveRange(db.Hubs);
        db.Customers.RemoveRange(db.Customers);
        db.SystemInfo.RemoveRange(db.SystemInfo);
        await db.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
        }
    }
}

/// <summary>
/// Authentication handler that automatically authenticates all requests as "admin".
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[] { new Claim(ClaimTypes.Name, "admin") };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "TestScheme");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
