namespace ObentoTensho.Contracts.Dtos.Reservations;

public sealed record AdminReservationDetailDto(AdminReservationDto Reservation, IReadOnlyList<ReservationHistoryDto> History);
