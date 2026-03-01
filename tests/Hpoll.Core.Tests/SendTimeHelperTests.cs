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
    public void ComputeNextSendTimeUtc_EmptyLocalTimesAndNoDefaults_FallsBackTo0800Utc()
    {
        var now = new DateTime(2026, 3, 1, 3, 0, 0, DateTimeKind.Utc);
        var result = SendTimeHelper.ComputeNextSendTimeUtc("", "UTC", now);

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 3, 1, 8, 0, 0), result.Value);
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

    [Fact]
    public void ComputeNextSendTimeUtc_DstSpringForwardGap_HandlesInvalidTime()
    {
        // US Eastern: Spring forward on March 8, 2026 at 2:00 AM → 3:00 AM
        // 2:30 AM local is invalid (doesn't exist), should be handled gracefully
        var now = new DateTime(2026, 3, 8, 6, 0, 0, DateTimeKind.Utc); // 1:00 AM EST
        var result = SendTimeHelper.ComputeNextSendTimeUtc("02:30", "America/New_York", now);

        Assert.NotNull(result);
        // The time should be adjusted past the gap
        Assert.True(result.Value > now);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_DstFallBack_HandlesAmbiguousTime()
    {
        // US Eastern: Fall back on Nov 1, 2026 at 2:00 AM → 1:00 AM
        // 1:30 AM local is ambiguous, but should still resolve
        var now = new DateTime(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc); // before 1:30 AM EST
        var result = SendTimeHelper.ComputeNextSendTimeUtc("01:30", "America/New_York", now);

        Assert.NotNull(result);
        Assert.True(result.Value > now);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_DefaultUtcTimes_AllInvalid_ReturnsNull()
    {
        var now = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var defaults = new List<string> { "invalid", "bad" };
        var result = SendTimeHelper.ComputeNextSendTimeUtc("", "UTC", now, defaults);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_EmptyDefaultList_FallsBackToLastResort()
    {
        var now = new DateTime(2026, 3, 1, 3, 0, 0, DateTimeKind.Utc);
        var result = SendTimeHelper.ComputeNextSendTimeUtc("", "UTC", now, new List<string>());

        Assert.NotNull(result);
        // Falls back to 08:00 UTC
        Assert.Equal(new DateTime(2026, 3, 1, 8, 0, 0), result.Value);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_SingleTimeToday_NotYetPassed_ReturnsToday()
    {
        var now = new DateTime(2026, 3, 1, 7, 0, 0, DateTimeKind.Utc);
        var result = SendTimeHelper.ComputeNextSendTimeUtc("08:00", "UTC", now);

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 3, 1, 8, 0, 0), result.Value);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_SingleTimeToday_AlreadyPassed_ReturnsTomorrow()
    {
        var now = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var result = SendTimeHelper.ComputeNextSendTimeUtc("08:00", "UTC", now);

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 3, 2, 8, 0, 0), result.Value);
    }

    [Fact]
    public void ParseTimeSpans_HandlesExtraWhitespace()
    {
        var result = SendTimeHelper.ParseTimeSpans("  06:00 , 12:00 ,  19:00  ");

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void ParseTimeSpans_SingleEntry()
    {
        var result = SendTimeHelper.ParseTimeSpans("14:30");

        Assert.Single(result);
        Assert.Contains(new TimeSpan(14, 30, 0), result);
    }

    [Fact]
    public void ComputeNextSendTimeUtc_DefaultUtcTimes_SingleTime_WrapsToTomorrow()
    {
        var now = new DateTime(2026, 3, 1, 23, 0, 0, DateTimeKind.Utc);
        var defaults = new List<string> { "08:00" };
        var result = SendTimeHelper.ComputeNextSendTimeUtc("", "UTC", now, defaults);

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 3, 2, 8, 0, 0), result.Value);
    }
}
