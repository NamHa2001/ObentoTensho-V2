namespace ObentoTensho.Contracts.Dtos.Customers;

/// <summary>Xuất danh sách: lý do bắt buộc, chạy nền, mỗi người một lần/ngày (BR-CUS-07).</summary>
public sealed record DataExportRequest(string Reason);
