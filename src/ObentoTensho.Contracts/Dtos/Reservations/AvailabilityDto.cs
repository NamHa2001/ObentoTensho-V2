namespace ObentoTensho.Contracts.Dtos.Reservations;

/// <summary>Lịch nhận đặt bàn của một ngày (giờ VN). <c>Open</c> false khi quán nghỉ hoặc cả ngày bị khóa.</summary>
public sealed record AvailabilityDto(DateOnly Date, bool Open, IReadOnlyList<SlotAvailabilityDto> Slots);
