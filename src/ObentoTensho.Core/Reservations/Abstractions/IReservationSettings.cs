namespace ObentoTensho.Core.Reservations.Abstractions;

// Núm vặn `reservation.*` (docs/srs/19 §3); khi có bảng cài đặt sẽ đọc từ đó thay cho cấu hình
public interface IReservationSettings
{
    int MaxGuestsPerBooking { get; }

    int MaxAdvanceDays { get; }

    int MaxActivePerPhone { get; }

    bool RequireContactVerify { get; }

    int CallAttempts { get; }

    int NoShowFlagCount { get; }

    int LargePartySize { get; }

    // Khoảng cách giữa các giờ đến khách chọn được trong một ca
    int TimeStepMinutes { get; }
}
