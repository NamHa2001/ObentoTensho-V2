using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Vouchers;

/// <summary>Kết quả sinh hàng loạt: mã lô để duyệt/lọc, các mã đã tạo.</summary>
public sealed record BulkGenerateResultDto(Guid BatchId, int Count, ApprovalStatus ApprovalStatus, IReadOnlyList<string> Codes);
