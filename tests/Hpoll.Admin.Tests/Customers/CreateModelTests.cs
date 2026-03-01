using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Hpoll.Admin.Pages.Customers;
using Hpoll.Core.Configuration;
using Hpoll.Data;

namespace Hpoll.Admin.Tests.Customers;

public class CreateModelTests : IDisposable
{
    private readonly HpollDbContext _db;

    public CreateModelTests()
    {
        var options = new DbContextOptionsBuilder<HpollDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new HpollDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private CreateModel CreatePageModel()
    {
        var emailSettings = Options.Create(new EmailSettings());
        var model = new CreateModel(_db, emailSettings);
        model.PageContext = new PageContext
        {
            ActionDescriptor = new CompiledPageActionDescriptor(),
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData()
        };
        return model;
    }

    [Fact]
    public async Task OnPostAsync_ValidInput_CreatesCustomer()
    {
        var model = CreatePageModel();
        model.Name = "Alice Smith";
        model.Email = "alice@example.com";
        model.TimeZoneId = "UTC";

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Detail", redirect.PageName);

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == "alice@example.com");
        Assert.NotNull(customer);
        Assert.Equal("Alice Smith", customer.Name);
        Assert.Equal("active", customer.Status);
    }

    [Fact]
    public async Task OnPostAsync_ValidInput_SetsSendTimesAndNextSendTime()
    {
        var model = CreatePageModel();
        model.Name = "Alice Smith";
        model.Email = "alice@example.com";
        model.TimeZoneId = "UTC";
        model.SendTimesLocal = "19:30";

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == "alice@example.com");
        Assert.NotNull(customer);
        Assert.Equal("19:30", customer.SendTimesLocal);
        Assert.NotNull(customer.NextSendTimeUtc);
    }

    [Fact]
    public void OnPostAsync_DefaultSendTime_Is1930()
    {
        var model = CreatePageModel();
        Assert.Equal("19:30", model.SendTimesLocal);
    }

    [Fact]
    public async Task OnPostAsync_InvalidTimezone_ReturnsError()
    {
        var model = CreatePageModel();
        model.Name = "Bob";
        model.Email = "bob@example.com";
        model.TimeZoneId = "Invalid/Timezone";

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("TimeZoneId"));
    }

    [Fact]
    public async Task OnPostAsync_InvalidSendTimes_ReturnsError()
    {
        var model = CreatePageModel();
        model.Name = "Bob";
        model.Email = "bob@example.com";
        model.TimeZoneId = "UTC";
        model.SendTimesLocal = "invalid";

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey("SendTimesLocal"));
    }

    [Fact]
    public async Task OnPostAsync_InvalidModelState_ReturnsPage()
    {
        var model = CreatePageModel();
        model.ModelState.AddModelError("Name", "Name is required.");

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_EmptySendTimes_UsesDefaultAndSetsNextSendTime()
    {
        var model = CreatePageModel();
        model.Name = "Alice Smith";
        model.Email = "alice@example.com";
        model.TimeZoneId = "UTC";
        model.SendTimesLocal = "";

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == "alice@example.com");
        Assert.NotNull(customer);
        Assert.Equal("", customer.SendTimesLocal);
        Assert.NotNull(customer.NextSendTimeUtc); // Should fall back to default
    }
}
