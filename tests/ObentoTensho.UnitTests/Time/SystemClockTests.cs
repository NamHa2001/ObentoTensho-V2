using Microsoft.Extensions.Time.Testing;
using ObentoTensho.Infrastructure.Time;

namespace ObentoTensho.UnitTests.Time;

public sealed class SystemClockTests
{
    private static SystemClock ClockAt(DateTimeOffset utc) => new(new FakeTimeProvider(utc));

    [Fact]
    public void UtcNow_returns_utc_kind()
    {
        var clock = ClockAt(new DateTimeOffset(2026, 11, 6, 3, 0, 0, TimeSpan.Zero));

        Assert.Equal(DateTimeKind.Utc, clock.UtcNow.Kind);
        Assert.Equal(new DateTime(2026, 11, 6, 3, 0, 0, DateTimeKind.Utc), clock.UtcNow);
    }

    // TIME-04: 17:00 UTC đã là 00:00 ngày hôm sau ở Việt Nam
    [Theory]
    [InlineData(16, 59, 6)]
    [InlineData(17, 0, 7)]
    [InlineData(23, 59, 7)]
    public void TodayLocal_switches_at_vietnam_midnight(int utcHour, int utcMinute, int expectedDay)
    {
        var clock = ClockAt(new DateTimeOffset(2026, 11, 6, utcHour, utcMinute, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 11, expectedDay), clock.TodayLocal);
    }

    [Fact]
    public void ToLocal_adds_seven_hours()
    {
        var clock = ClockAt(DateTimeOffset.UnixEpoch);

        var local = clock.ToLocal(new DateTime(2026, 12, 31, 18, 30, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2027, 1, 1, 1, 30, 0), local);
    }

    [Fact]
    public void ToLocal_rejects_non_utc()
    {
        var clock = ClockAt(DateTimeOffset.UnixEpoch);

        Assert.Throws<ArgumentException>(() => clock.ToLocal(new DateTime(2026, 11, 6, 8, 0, 0, DateTimeKind.Local)));
        Assert.Throws<ArgumentException>(() => clock.ToLocal(new DateTime(2026, 11, 6, 8, 0, 0, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void ToUtc_converts_vietnam_time_back_to_utc()
    {
        var clock = ClockAt(DateTimeOffset.UnixEpoch);

        var utc = clock.ToUtc(new DateOnly(2026, 11, 7), new TimeOnly(6, 0));

        Assert.Equal(new DateTime(2026, 11, 6, 23, 0, 0, DateTimeKind.Utc), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }
}
