namespace ObentoTensho.Core.Common;

// Việc hằng ngày tới hạn khi đã qua giờ chạy (giờ VN) và hôm nay chưa chạy xong — gồm cả chạy bù khi máy tắt đúng giờ
public static class DailySchedule
{
    public static bool IsDue(TimeOnly nowLocal, TimeOnly runAtLocal, DateOnly todayLocal, DateOnly? lastRunLocalDate) =>
        nowLocal >= runAtLocal && (lastRunLocalDate is not { } last || last < todayLocal);
}
