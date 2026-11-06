namespace ObentoTensho.Contracts.Dtos.Reservations;

/// <summary>Khách đặt bàn (FR-RSV-01). Không cần tài khoản; nếu đang đăng nhập thì gắn vào tài khoản.</summary>
public sealed record CreateReservationRequest(
    DateOnly Date,
    TimeOnly Time,
    int AdultCount,
    int ChildCount,
    string GuestName,
    string GuestPhone,
    string GuestEmail,
    string? SpecialRequest,
    string? Lang);
