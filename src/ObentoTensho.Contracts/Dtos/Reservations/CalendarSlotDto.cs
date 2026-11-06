namespace ObentoTensho.Contracts.Dtos.Reservations;

public sealed record CalendarSlotDto(int SlotId, TimeOnly Start, TimeOnly End, int BookedCount, int MaxReservations, int BookedGuests, int? MaxGuests);
