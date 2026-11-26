namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Xóa bill đã hoàn thành vì nhập sai: lý do bắt buộc (văn bản thuần, tối đa 300 ký tự) để quản lý hỏi lại.</summary>
public sealed record DeleteOrderRequest(string? Reason);
