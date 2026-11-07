namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Các giờ lấy món còn chọn được trong một ngày (giờ Việt Nam, BR-ORD-05).</summary>
public sealed record PickupTimesDto(DateOnly Date, IReadOnlyList<TimeOnly> Times);
