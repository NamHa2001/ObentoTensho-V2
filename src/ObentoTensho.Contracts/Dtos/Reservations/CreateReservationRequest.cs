namespace ObentoTensho.Contracts.Dtos.Reservations;

/// <summary>
/// Khách đặt bàn (FR-RSV-01). Không cần tài khoản; nếu đang đăng nhập thì gắn vào tài khoản.
/// <c>OtpCode</c>: chỉ cần khi lần gửi trước trả 409 OTP_REQUIRED — mã 6 số gửi tới <c>GuestEmail</c> (BR-RSV-21).
/// </summary>
public sealed record CreateReservationRequest(
    DateOnly Date,
    TimeOnly Time,
    int AdultCount,
    int ChildCount,
    string GuestName,
    string GuestPhone,
    string GuestEmail,
    string? SpecialRequest,
    string? Lang,
    string? OtpCode = null);
