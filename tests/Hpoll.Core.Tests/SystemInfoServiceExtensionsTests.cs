using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Hpoll.Core.Interfaces;

namespace Hpoll.Core.Tests;

public class SystemInfoServiceExtensionsTests
{
    [Fact]
    public async Task TrySetBatchAsync_CallsSetBatchAsync()
    {
        var mockService = new Mock<ISystemInfoService>();
        var logger = NullLogger.Instance;

        var entries = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        await mockService.Object.TrySetBatchAsync("Runtime", entries, logger);

        mockService.Verify(s => s.SetBatchAsync("Runtime",
            It.Is<Dictionary<string, string>>(d => d.Count == 2 && d["key1"] == "value1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TrySetBatchAsync_SwallowsExceptions()
    {
        var mockService = new Mock<ISystemInfoService>();
        mockService.Setup(s => s.SetBatchAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var logger = NullLogger.Instance;

        // Should not throw
        await mockService.Object.TrySetBatchAsync("Runtime", new Dictionary<string, string>
        {
            ["key"] = "value"
        }, logger);
    }

    [Fact]
    public async Task TrySetBatchAsync_LogsWarningOnException()
    {
        var mockService = new Mock<ISystemInfoService>();
        mockService.Setup(s => s.SetBatchAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var mockLogger = new Mock<ILogger>();

        await mockService.Object.TrySetBatchAsync("Runtime", new Dictionary<string, string>
        {
            ["key"] = "value"
        }, mockLogger.Object);

        mockLogger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task TrySetBatchAsync_PassesCancellationToken()
    {
        var mockService = new Mock<ISystemInfoService>();
        var logger = NullLogger.Instance;
        var cts = new CancellationTokenSource();

        await mockService.Object.TrySetBatchAsync("Runtime", new Dictionary<string, string>
        {
            ["key"] = "value"
        }, logger, cts.Token);

        mockService.Verify(s => s.SetBatchAsync("Runtime",
            It.IsAny<Dictionary<string, string>>(),
            cts.Token), Times.Once);
    }
}
