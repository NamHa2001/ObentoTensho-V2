namespace ObentoTensho.Core.Reservations.Abstractions;

// Núm vặn `reservation.*` (docs/srs/19 §3)
public interface IReservationSettings
{
    int MaxGuestsPerBooking { get; }

    int MaxAdvanceDays { get; }

    int MaxActivePerPhone { get; }

    // BR-RSV-06: 0 = hủy tự do; > 0 = chỉ hủy được trước giờ hẹn bấy nhiêu giờ
    int CancellationHours { get; }

    bool RequireContactVerify { get; }

    int CallAttempts { get; }

    int NoShowFlagCount { get; }

    int LargePartySize { get; }

    // Khoảng cách giữa các giờ đến khách chọn được trong một ca
    int TimeStepMinutes { get; }

    // BR-RSV-24: quá giờ hẹn bấy nhiêu phút chưa check-in ⇒ tự No-show
    int GraceMinutes { get; }

    // BR-RSV-25: còn bấy nhiêu phút tới giờ hẹn mà vẫn Pending ⇒ tự hủy, xin lỗi khách
    int PendingCutoffMinutes { get; }

    // BR-RSV-25: chờ xác nhận quá bấy nhiêu phút thì nhắc nhân viên
    int PendingAlertMinutes { get; }

    // BR-RSV-08: nhắc trước giờ hẹn khoảng bấy nhiêu phút
    int ReminderLeadMinutes { get; }
}
