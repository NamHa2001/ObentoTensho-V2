namespace ObentoTensho.Contracts.Dtos.DineIn;

/// <summary>Mở bàn: bàn đang trống (hoặc khách đặt trước đã check-in), số khách 1–50.</summary>
public sealed record OpenTableRequest(int TableId, int GuestCount, string? Note);
