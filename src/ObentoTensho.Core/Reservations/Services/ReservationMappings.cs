using ObentoTensho.Contracts.Dtos.Reservations;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Reservations.Domain;

namespace ObentoTensho.Core.Reservations.Services;

// Thực thể → DTO (AR-08). Bản công khai luôn che liên hệ, không có ghi chú nội bộ (SEC-01 d).
public static class ReservationMappings
{
    public static PublicReservationDto ToPublic(this Reservation r) =>
        new(
            r.ReservationCode,
            r.ReservedDate,
            r.ReservedTime,
            r.AdultCount,
            r.ChildCount,
            r.Status,
            r.GuestName,
            PiiMask.Phone(r.GuestPhone),
            r.GuestEmail is null ? string.Empty : PiiMask.Email(r.GuestEmail),
            r.SpecialRequest);

    public static AdminReservationDto ToAdmin(this Reservation r, bool noShowFlag, int largePartySize) =>
        new(
            r.Id,
            r.ReservationCode,
            r.ReservedDate,
            r.ReservedTime,
            r.SlotId,
            r.AdultCount,
            r.ChildCount,
            r.Status,
            r.Source,
            r.GuestName,
            PiiMask.Phone(r.GuestPhone),
            r.GuestEmail is null ? null : PiiMask.Email(r.GuestEmail),
            r.SpecialRequest,
            r.InternalNote,
            r.TableId,
            r.ContactConfirmedAtUtc is not null,
            r.ConfirmNote,
            r.CallAttempts,
            noShowFlag,
            r.GuestCount >= largePartySize,
            RowVersions.Encode(r.RowVersion));

    public static ReservationHistoryDto ToDto(this ReservationStatusHistory h) =>
        new(h.FromStatus, h.ToStatus, h.ChangedByType, h.ChangedById, h.Reason, h.ChangedAtUtc);
}
