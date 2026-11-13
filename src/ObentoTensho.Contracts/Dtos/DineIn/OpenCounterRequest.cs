namespace ObentoTensho.Contracts.Dtos.DineIn;

/// <summary>Đơn mua mang về tại quầy: tên để gọi khách khi xong món (không bắt buộc), ghi chú.</summary>
public sealed record OpenCounterRequest(string? CustomerName, string? Note);
