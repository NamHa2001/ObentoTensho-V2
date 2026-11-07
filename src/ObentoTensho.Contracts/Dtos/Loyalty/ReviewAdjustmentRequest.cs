namespace ObentoTensho.Contracts.Dtos.Loyalty;

/// <summary>Duyệt/từ chối điều chỉnh điểm đang chờ; người duyệt phải khác người đề nghị.</summary>
public sealed record ReviewAdjustmentRequest(string? Note);
