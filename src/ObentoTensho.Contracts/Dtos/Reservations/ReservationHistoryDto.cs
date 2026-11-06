using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Reservations;

public sealed record ReservationHistoryDto(
    ReservationStatus? FromStatus,
    ReservationStatus ToStatus,
    ReservationActor ChangedBy,
    Guid? ChangedById,
    string? Reason,
    DateTime ChangedAtUtc);
