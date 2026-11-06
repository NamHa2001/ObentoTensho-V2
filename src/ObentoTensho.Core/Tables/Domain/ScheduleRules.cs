using ObentoTensho.Contracts.Dtos.Tables;

namespace ObentoTensho.Core.Tables.Domain;

// BR-TBL-02, BR-TBL-03, BR-TBL-12: ràng buộc giữa các khoảng mở cửa và ca phục vụ
public static class ScheduleRules
{
    public const int MaxRangesPerDay = 4;

    // Một tuần mẫu bắt đầu Chủ nhật để đặt các khoảng lên trục thời gian thật
    private static readonly DateOnly ReferenceSunday = new(2026, 1, 4);

    public static DateOnly ReferenceDate(int dayOfWeek) => ReferenceSunday.AddDays(dayOfWeek);

    // Các khoảng của cả tuần (kể cả khoảng thứ Bảy qua nửa đêm chạm Chủ nhật tuần sau) không được chồng nhau
    public static bool WeeklyRangesOverlap(IEnumerable<OpeningDayDto> days)
    {
        var intervals = days
            .Where(d => !d.Closed)
            .SelectMany(d => d.Ranges.SelectMany(r => new[]
            {
                OpeningSchedule.ToInterval(ReferenceDate(d.DayOfWeek), r.Open, r.Close, r.CrossesMidnight),
                OpeningSchedule.ToInterval(ReferenceDate(d.DayOfWeek + 7), r.Open, r.Close, r.CrossesMidnight),
            }))
            .OrderBy(i => i.Start)
            .ToArray();

        return intervals.Zip(intervals.Skip(1)).Any(pair => pair.First.Overlaps(pair.Second));
    }

    public static bool RangesOverlap(IEnumerable<OpeningRangeDto> ranges)
    {
        var sorted = ranges.OrderBy(r => r.Open).ToArray();
        return sorted.Zip(sorted.Skip(1)).Any(pair => pair.Second.Open < pair.First.Close);
    }

    public static bool SlotsOverlap(ServiceSlot a, ServiceSlot b) =>
        (a.DayOfWeek is null || b.DayOfWeek is null || a.DayOfWeek == b.DayOfWeek)
        && a.StartTime < b.EndTime && b.StartTime < a.EndTime;

    // Ca phải nằm trọn trong một khoảng mở ở mọi ngày nó áp dụng mà quán có mở;
    // ca của một ngày cụ thể mà ngày đó nghỉ thì không hợp lệ
    public static bool SlotFitsOpeningHours(ServiceSlot slot, IReadOnlyCollection<OpeningHour> weekly)
    {
        var schedule = new OpeningSchedule(weekly, []);
        var days = slot.DayOfWeek is { } day ? [day] : Enumerable.Range(0, 7).Select(d => (byte)d).ToArray();
        var openDays = days.Where(d => schedule.IntervalsCovering(ReferenceDate(d)).Count > 0).ToArray();

        return openDays.Length > 0
            && openDays.All(d => schedule.IsOpen(ReferenceDate(d), slot.StartTime, slot.EndTime));
    }
}
