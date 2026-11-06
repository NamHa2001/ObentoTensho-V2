using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Core.Reservations.Services;

// Dữ liệu đã kiểm tra, sẵn sàng giữ chỗ và lưu (SĐT/email đã chuẩn hóa)
public sealed record BookingDraft(
    DateOnly Date,
    TimeOnly Time,
    ServiceSlot Slot,
    int AdultCount,
    int ChildCount,
    string GuestName,
    string GuestPhone,
    string? GuestEmail,
    string? SpecialRequest,
    string? InternalNote,
    string Lang,
    ReservationSource Source,
    Guid? UserId,
    Guid? StaffId,
    ReservationStatus InitialStatus,
    string? OverrideCapacityReason,
    bool ContactVerified = false);
