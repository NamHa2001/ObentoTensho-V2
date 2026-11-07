namespace ObentoTensho.Contracts.Dtos.Customers;

/// <summary>Token lấy từ liên kết trong thư; chỉ người yêu cầu tải được.</summary>
public sealed record DataExportDownloadRequest(string Token);
