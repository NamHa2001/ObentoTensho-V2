namespace ObentoTensho.Contracts.Dtos.DineIn;

/// <summary>Gộp phiếu của bàn khác vào phiếu này; phiếu nguồn bị hủy, bàn nguồn chuyển sang dọn dẹp.</summary>
public sealed record MergeBillRequest(Guid FromBillId);
