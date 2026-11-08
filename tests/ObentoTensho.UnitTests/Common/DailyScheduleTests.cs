using ObentoTensho.Core.Common;

namespace ObentoTensho.UnitTests.Common;

// TIME-08: việc hằng ngày chạy từ giờ VN cố định, mỗi ngày một lần, chạy bù khi máy tắt đúng giờ
public sealed class DailyScheduleTests
{
    private static readonly DateOnly Today = new(2026, 11, 8);
    private static readonly TimeOnly NineAm = new(9, 0);

    [Theory]
    [InlineData("08:59", null, false)]
    [InlineData("09:00", null, true)]
    [InlineData("23:30", "2026-11-07", true)]
    [InlineData("09:15", "2026-11-08", false)]
    [InlineData("14:00", "2026-11-01", true)]
    public void Toi_gio_va_hom_nay_chua_chay(string now, string? lastRun, bool due)
    {
        var last = lastRun is null ? (DateOnly?)null : DateOnly.Parse(lastRun, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(due, DailySchedule.IsDue(TimeOnly.Parse(now, System.Globalization.CultureInfo.InvariantCulture), NineAm, Today, last));
    }
}
