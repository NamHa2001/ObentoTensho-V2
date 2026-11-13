namespace ObentoTensho.Contracts.Dtos.DineIn;

/// <summary>Hủy món đã gửi bếp (cần quyền quản lý) hoặc hủy cả phiếu bàn: lý do bắt buộc.</summary>
public sealed record DineInReasonRequest(string Reason);
