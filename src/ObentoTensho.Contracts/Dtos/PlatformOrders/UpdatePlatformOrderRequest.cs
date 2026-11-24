namespace ObentoTensho.Contracts.Dtos.PlatformOrders;

// docs/srs/23 §5: sửa mã đơn app hoặc giảm giá; đơn đã hoàn thành tính lại theo tỉ lệ đã chụp
public sealed record UpdatePlatformOrderRequest(string? ExternalOrderCode, decimal? DiscountPercent, decimal? DiscountAmount);
