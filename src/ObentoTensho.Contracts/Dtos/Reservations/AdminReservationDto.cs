using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Reservations;

/// <summary>
/// Đặt bàn cho màn hình lễ tân. SĐT/email luôn ở dạng che; xem đầy đủ qua "Hiện liên hệ" (có audit — BR-CUS-09).
/// <c>NoShowFlag</c>: SĐT có lịch sử không đến — cần xác nhận kỹ (BR-RSV-22).
/// </summary>
public sealed record AdminReservationDto(
    Guid Id,
    string Code,
    DateOnly Date,
    TimeOnly Time,
    int SlotId,
    int AdultCount,
    int ChildCount,
    ReservationStatus Status,
    ReservationSource Source,
    string GuestName,
    string MaskedPhone,
    string? MaskedEmail,
    string? SpecialRequest,
    string? InternalNote,
    int? TableId,
    bool ContactConfirmed,
    string? ConfirmNote,
    int CallAttempts,
    bool NoShowFlag,
    bool LargeParty,
    string Version);
