namespace ObentoTensho.Contracts.Dtos.Reservations;

/// <summary>
/// Một ca: số lượt/khách còn nhận được và các giờ đến có thể chọn (bước 15 phút).
/// <c>RemainingGuests</c> null khi ca không giới hạn số khách.
/// </summary>
public sealed record SlotAvailabilityDto(
    int SlotId,
    TimeOnly Start,
    TimeOnly End,
    int RemainingReservations,
    int? RemainingGuests,
    IReadOnlyList<TimeOnly> Times);
