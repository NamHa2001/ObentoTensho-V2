namespace ObentoTensho.Core.Tables.Domain;

// Tính các khoảng mở cửa thực tế của một ngày (giờ VN — BR-TBL-01) từ lịch tuần và giờ đặc biệt.
// Khoảng qua nửa đêm của hôm trước cũng được tính vào phần đầu ngày hôm nay.
public sealed class OpeningSchedule(IEnumerable<OpeningHour> weekly, IEnumerable<SpecialHour> special)
{
    private readonly OpeningHour[] _weekly = weekly.ToArray();
    private readonly ILookup<DateOnly, SpecialHour> _special = special.ToLookup(s => s.Date);

    // Các khoảng bắt đầu trong ngày `date` (có thể kết thúc sang ngày sau)
    public IReadOnlyList<OpenInterval> IntervalsStartingOn(DateOnly date)
    {
        if (_special.Contains(date))
        {
            return _special[date]
                .Where(s => !s.IsClosed && s.OpenTime is not null && s.CloseTime is not null)
                .Select(s => new OpenInterval(date.ToDateTime(s.OpenTime.GetValueOrDefault()), date.ToDateTime(s.CloseTime.GetValueOrDefault())))
                .OrderBy(i => i.Start)
                .ToArray();
        }

        var day = (byte)date.DayOfWeek;
        return _weekly
            .Where(h => h.DayOfWeek == day && !h.IsClosed)
            .Select(h => ToInterval(date, h.OpenTime, h.CloseTime, h.CrossesMidnight))
            .OrderBy(i => i.Start)
            .ToArray();
    }

    // Mọi khoảng có phần nằm trong ngày `date`, gồm cả khoảng qua nửa đêm của hôm trước
    public IReadOnlyList<OpenInterval> IntervalsCovering(DateOnly date)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        return IntervalsStartingOn(date.AddDays(-1))
            .Where(i => i.End > dayStart)
            .Concat(IntervalsStartingOn(date))
            .ToArray();
    }

    public bool IsOpen(DateOnly date, TimeOnly start, TimeOnly end)
    {
        var from = date.ToDateTime(start);
        var to = end > start ? date.ToDateTime(end) : date.AddDays(1).ToDateTime(end);
        return IntervalsCovering(date).Any(i => i.Contains(from, to));
    }

    public static OpenInterval ToInterval(DateOnly date, TimeOnly open, TimeOnly close, bool crossesMidnight) =>
        new(date.ToDateTime(open), crossesMidnight ? date.AddDays(1).ToDateTime(close) : date.ToDateTime(close));
}
