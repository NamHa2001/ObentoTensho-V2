using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Reservations;

/// <summary>
/// Lễ tân tạo hộ khách (FR-RSV-06). Email tùy chọn (khách gọi điện thường không cho email).
/// <c>OverrideCapacityReason</c>: chỉ Manager+ được vượt sức chứa, bắt buộc lý do.
/// </summary>
public sealed record StaffCreateReservationRequest(
    DateOnly Date,
    TimeOnly Time,
    int AdultCount,
    int ChildCount,
    string GuestName,
    string GuestPhone,
    string? GuestEmail,
    string? SpecialRequest,
    string? InternalNote,
    ReservationSource Source,
    bool CheckInNow,
    string? OverrideCapacityReason);
