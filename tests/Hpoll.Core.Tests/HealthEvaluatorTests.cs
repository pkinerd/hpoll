using Microsoft.Extensions.Options;
using Hpoll.Core.Configuration;
using Hpoll.Core.Services;
using Xunit;

namespace Hpoll.Core.Tests;

public class HealthEvaluatorTests
{
    private readonly HealthEvaluator _evaluator = new(Options.Create(new PollingSettings()));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void IsHubHealthy_WhenLessThan3Failures_ReturnsTrue(int failures)
    {
        var result = _evaluator.IsHubHealthy(failures);

        Assert.True(result);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(100)]
    public void IsHubHealthy_When3OrMoreFailures_ReturnsFalse(int failures)
    {
        var result = _evaluator.IsHubHealthy(failures);

        Assert.False(result);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void NeedsAttention_When3OrMoreFailures_ReturnsTrue(int failures)
    {
        var result = _evaluator.NeedsAttention(DateTime.UtcNow, failures);

        Assert.True(result);
    }

    [Fact]
    public void NeedsAttention_WhenLastSuccessOverSixHoursAgo_ReturnsTrue()
    {
        var lastSuccess = DateTime.UtcNow.AddHours(-7);

        var result = _evaluator.NeedsAttention(lastSuccess, 0);

        Assert.True(result);
    }

    [Fact]
    public void NeedsAttention_WhenRecentSuccessAndFewFailures_ReturnsFalse()
    {
        var lastSuccess = DateTime.UtcNow.AddMinutes(-30);

        var result = _evaluator.NeedsAttention(lastSuccess, 1);

        Assert.False(result);
    }

    [Fact]
    public void NeedsAttention_WhenLastSuccessNull_And_FewFailures_ReturnsFalse()
    {
        var result = _evaluator.NeedsAttention(null, 2);

        Assert.False(result);
    }

    [Fact]
    public void NeedsAttention_WhenLastSuccessNull_And_ManyFailures_ReturnsTrue()
    {
        var result = _evaluator.NeedsAttention(null, 5);

        Assert.True(result);
    }

    [Fact]
    public void NeedsAttention_JustUnderSilenceThreshold_ReturnsFalse()
    {
        // 5 hours 59 minutes (just under the 6-hour threshold) should NOT trigger
        var lastSuccess = DateTime.UtcNow.AddHours(-5).AddMinutes(-59);

        var result = _evaluator.NeedsAttention(lastSuccess, 0);

        Assert.False(result);
    }
}
