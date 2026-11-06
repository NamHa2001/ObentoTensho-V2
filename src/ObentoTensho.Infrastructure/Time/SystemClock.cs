using ObentoTensho.Core.Time;

namespace ObentoTensho.Infrastructure.Time;

public sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    // TIME-11: Việt Nam không có giờ mùa hè, vẫn dùng TimeZoneInfo thay vì cộng 7 giờ thủ công
    public const string RestaurantTimeZoneId = "Asia/Ho_Chi_Minh";

    private static readonly TimeZoneInfo RestaurantTimeZone = TimeZoneInfo.FindSystemTimeZoneById(RestaurantTimeZoneId);

    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public DateOnly TodayLocal => DateOnly.FromDateTime(ToLocal(UtcNow));

    public DateTime ToLocal(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Thời điểm phải là UTC.", nameof(utc));
        }

        return TimeZoneInfo.ConvertTimeFromUtc(utc, RestaurantTimeZone);
    }

    public DateTime ToUtc(DateOnly localDate, TimeOnly localTime) =>
        TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(localTime, DateTimeKind.Unspecified), RestaurantTimeZone);
}
