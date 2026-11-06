namespace ObentoTensho.Contracts.Dtos.Tables;

/// <summary>Ca phục vụ; <c>DayOfWeek</c> null = mọi ngày.</summary>
public sealed record ServiceSlotDto(
    int Id,
    int? DayOfWeek,
    TimeOnly Start,
    TimeOnly End,
    int MaxReservations,
    int? MaxGuests,
    bool IsActive,
    string Version);
