using Hpoll.Core.Services;

namespace Hpoll.Core.Tests;

public class SendTimeHelperTests
{
    [Fact]
    public void ComputeNextSendTimeUtc_WithLocalTimes_PicksNextFutureTime()
    {
        // UTC timezone so local == UTC for simplicity
        var now = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var result = SendTimeHelper.ComputeNextSendTimeUtc("06:00, 12:00, 18:00", "UTC", now);

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 3, 1, 12, 0, 0), result.Value);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_WithLocalTimes_WrapsToTomorrow()
    {
        var now = new DateTime(2026, 3, 1, 20, 0, 0, DateTimeKind.Utc);
        var result = SendTimeHelper.ComputeNextSendTimeUtc("06:00, 12:00, 18:00", "UTC", now);

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 3, 2, 6, 0, 0), result.Value);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_WithLocalTimes_ConvertsTimezoneCorrectly()
    {
        // 19:30 AEST = 08:30 UTC (AEST is UTC+11 in March - daylight saving)
        // Actually, Australia/Sydney in March is AEDT (UTC+11)
        var now = new DateTime(2026, 3, 1, 7, 0, 0, DateTimeKind.Utc); // 18:00 AEDT
        var result = SendTimeHelper.ComputeNextSendTimeUtc("19:30", "Australia/Sydney", now);

        Assert.NotNull(result);
        // 19:30 AEDT = 08:30 UTC on March 1
        Assert.Equal(new DateTime(2026, 3, 1, 8, 30, 0), result.Value);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_WithLocalTimes_WrapsToTomorrowInTimezone()
    {
        // Now is 09:00 UTC = 20:00 AEDT, so 19:30 AEDT today has passed
        var now = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var result = SendTimeHelper.ComputeNextSendTimeUtc("19:30", "Australia/Sydney", now);

        Assert.NotNull(result);
        // Should be 19:30 AEDT on March 2 = 08:30 UTC on March 2
        Assert.Equal(new DateTime(2026, 3, 2, 8, 30, 0), result.Value);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_EmptyLocalTimes_FallsBackToDefaultUtc()
    {
        var now = new DateTime(2026, 3, 1, 3, 0, 0, DateTimeKind.Utc);
        var defaults = new List<string> { "06:00", "12:00", "18:00" };
        var result = SendTimeHelper.ComputeNextSendTimeUtc("", "UTC", now, defaults);

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 3, 1, 6, 0, 0), result.Value);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_EmptyLocalTimesAndNoDefaults_ReturnsNull()
    {
        var now = new DateTime(2026, 3, 1, 3, 0, 0, DateTimeKind.Utc);
        var result = SendTimeHelper.ComputeNextSendTimeUtc("", "UTC", now);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_InvalidTimezone_ReturnsNull()
    {
        var now = new DateTime(2026, 3, 1, 3, 0, 0, DateTimeKind.Utc);
        var result = SendTimeHelper.ComputeNextSendTimeUtc("19:30", "Invalid/Timezone", now);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_DefaultUtcTimes_PicksNextFuture()
    {
        var now = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var defaults = new List<string> { "06:00", "12:00", "18:00" };
        var result = SendTimeHelper.ComputeNextSendTimeUtc("", "UTC", now, defaults);

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 3, 1, 12, 0, 0), result.Value);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_DefaultUtcTimes_WrapsToTomorrow()
    {
        var now = new DateTime(2026, 3, 1, 20, 0, 0, DateTimeKind.Utc);
        var defaults = new List<string> { "06:00", "12:00", "18:00" };
        var result = SendTimeHelper.ComputeNextSendTimeUtc("", "UTC", now, defaults);

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 3, 2, 6, 0, 0), result.Value);
    }

    [Fact]
    public void ParseTimeSpans_ParsesValidTimes()
    {
        var result = SendTimeHelper.ParseTimeSpans("06:00, 12:30, 19:00");

        Assert.Equal(3, result.Count);
        Assert.Contains(new TimeSpan(6, 0, 0), result);
        Assert.Contains(new TimeSpan(12, 30, 0), result);
        Assert.Contains(new TimeSpan(19, 0, 0), result);
    }

    [Fact]
    public void ParseTimeSpans_SkipsInvalidEntries()
    {
        var result = SendTimeHelper.ParseTimeSpans("06:00, invalid, 19:00");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ParseTimeSpans_EmptyString_ReturnsEmptyList()
    {
        var result = SendTimeHelper.ParseTimeSpans("");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseTimeSpans_NullString_ReturnsEmptyList()
    {
        var result = SendTimeHelper.ParseTimeSpans(null!);
        Assert.Empty(result);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_MultipleSendTimes_SortsAndPicksCorrect()
    {
        // Unsorted input, should still pick the correct next time
        var now = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var result = SendTimeHelper.ComputeNextSendTimeUtc("18:00, 06:00, 12:00", "UTC", now);

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 3, 1, 12, 0, 0), result.Value);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_AllInvalidLocalTimes_ReturnsNull()
    {
        var now = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var result = SendTimeHelper.ComputeNextSendTimeUtc("invalid, bad", "UTC", now);

        Assert.Null(result);
    }
}
