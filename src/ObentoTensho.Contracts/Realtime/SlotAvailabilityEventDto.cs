namespace ObentoTensho.Contracts.Realtime;

/// <summary>Số lượt/khách còn nhận của một ca trong ngày đang xem. <c>AvailableGuests</c> null khi ca không giới hạn khách.</summary>
public sealed record SlotAvailabilityEventDto(DateOnly Date, int SlotId, int Available, int? AvailableGuests);
