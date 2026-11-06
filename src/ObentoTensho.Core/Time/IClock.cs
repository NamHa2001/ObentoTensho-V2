namespace ObentoTensho.Core.Time;

// TIME-02: nguồn thời gian duy nhất. Lưu trữ UTC; "hôm nay" và giờ hiển thị theo múi giờ của nhà hàng.
public interface IClock
{
    DateTime UtcNow { get; }

    DateOnly TodayLocal { get; }

    DateTime ToLocal(DateTime utc);

    DateTime ToUtc(DateOnly localDate, TimeOnly localTime);
}
