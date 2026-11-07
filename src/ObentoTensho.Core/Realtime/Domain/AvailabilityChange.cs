namespace ObentoTensho.Core.Realtime.Domain;

// Số chỗ còn tính lại sau commit lúc phát, để luôn là con số mới nhất
public sealed record AvailabilityChange(DateOnly Date, int SlotId);
