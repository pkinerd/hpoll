using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Hpoll.Admin.Pages.Customers;
using Hpoll.Data;
using Hpoll.Data.Entities;

namespace Hpoll.Admin.Tests.Customers;

public class IndexModelTests : IDisposable
{
    private readonly HpollDbContext _db;

    public IndexModelTests()
    {
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new HpollDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private IndexModel CreatePageModel()
    {
        var model = new IndexModel(_db);
        model.PageContext = new PageContext
        {
            ActionDescriptor = new CompiledPageActionDescriptor(),
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData()
        };
        return model;
    }

    [Fact]
    public async Task OnGetAsync_ReturnsEmptyList_WhenNoCustomers()
    {
        var model = CreatePageModel();

        await model.OnGetAsync();

        Assert.Empty(model.Customers);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsAllCustomers()
    {
        _db.Customers.Add(new Customer { Name = "Alice", Email = "alice@example.com" });
        _db.Customers.Add(new Customer { Name = "Bob", Email = "bob@example.com" });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync();

        Assert.Equal(2, model.Customers.Count);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsCustomersOrderedByName()
    {
        _db.Customers.Add(new Customer { Name = "Zara", Email = "zara@example.com" });
        _db.Customers.Add(new Customer { Name = "Alice", Email = "alice@example.com" });
        _db.Customers.Add(new Customer { Name = "Mike", Email = "mike@example.com" });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync();

        Assert.Equal("Alice", model.Customers[0].Name);
        Assert.Equal("Mike", model.Customers[1].Name);
        Assert.Equal("Zara", model.Customers[2].Name);
    }

    [Fact]
    public async Task OnGetAsync_IncludesHubs()
    {
        var customer = new Customer { Name = "Test", Email = "test@example.com" };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        _db.Hubs.Add(new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "bridge1",
            HueApplicationKey = "key",
            AccessToken = "token",
            RefreshToken = "refresh",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = "active"
        });
        _db.Hubs.Add(new Hub
        {
            CustomerId = customer.Id,
            HueBridgeId = "bridge2",
            HueApplicationKey = "key2",
            AccessToken = "token2",
            RefreshToken = "refresh2",
            TokenExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = "active"
        });
        await _db.SaveChangesAsync();

        var model = CreatePageModel();
        await model.OnGetAsync();

        Assert.Single(model.Customers);
        Assert.Equal(2, model.Customers[0].Hubs.Count);
    }
}
