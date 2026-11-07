using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>
/// Đổi trạng thái; <c>ExpectedStatus</c> chặn hai người xử lý cùng lúc (DB-23). Hủy bắt buộc lý do.
/// <c>NoPickup</c>: hủy đơn <c>Ready</c> vì khách không đến lấy — tính vào cờ của SĐT (BR-ORD-18).
/// </summary>
public sealed record ChangeOrderStatusRequest(OrderStatus ExpectedStatus, OrderStatus Status, string? Reason, bool NoPickup = false);
