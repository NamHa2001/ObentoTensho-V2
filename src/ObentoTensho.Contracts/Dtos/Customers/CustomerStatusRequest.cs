namespace ObentoTensho.Contracts.Dtos.Customers;

/// <summary>Khóa hoặc mở khóa; lý do bắt buộc (BR-CUS-08).</summary>
public sealed record CustomerStatusRequest(bool IsActive, string Reason);
