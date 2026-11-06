namespace ObentoTensho.Contracts.Dtos.Tables;

public sealed record ServiceSlotRequest(int? DayOfWeek, TimeOnly Start, TimeOnly End, int MaxReservations, int? MaxGuests, bool IsActive);
