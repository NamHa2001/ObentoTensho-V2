namespace ObentoTensho.Contracts.Dtos.Loyalty;

/// <summary>Điều chỉnh điểm thủ công (+/−), lý do bắt buộc (BR-LOY-11). Vượt ngưỡng duyệt ⇒ chờ người thứ hai (BR-LOY-21).</summary>
public sealed record AdjustPointsRequest(int Points, string Reason);
