using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Đổi trạng thái; <c>ExpectedStatus</c> chặn hai người xử lý cùng lúc (DB-23). Hủy bắt buộc lý do.</summary>
public sealed record ChangeOrderStatusRequest(OrderStatus ExpectedStatus, OrderStatus Status, string? Reason);
