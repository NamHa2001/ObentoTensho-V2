using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>
/// Đơn cho thu ngân/quản lý. SĐT/email luôn che; xem đầy đủ qua "Hiện liên hệ" (có audit).
/// <c>NeedsContactConfirm</c>: phải gọi xác nhận trước khi bếp làm (BR-ORD-21).
/// <c>RefundPending</c>: đơn đã thanh toán bị hủy mà chưa ghi hoàn tiền (BR-ORD-16).
/// </summary>
public sealed record AdminOrderDto(
    Guid Id,
    string Code,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    DateOnly PickupDate,
    TimeOnly PickupTime,
    string CustomerName,
    string MaskedPhone,
    string MaskedEmail,
    decimal TotalAmount,
    int ItemCount,
    bool NeedsContactConfirm,
    bool ContactConfirmed,
    bool RefundPending,
    DateTime CreatedAtUtc,
    string Version);
