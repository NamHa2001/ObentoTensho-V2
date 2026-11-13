using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Đơn trả cho khách (BR-ORD-14): SĐT/email đã che. <c>CanCancel</c>: còn tự hủy được (BR-ORD-17).</summary>
public sealed record PublicOrderDto(
    string Code,
    OrderStatus Status,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    DateOnly PickupDate,
    TimeOnly PickupTime,
    string CustomerName,
    string MaskedPhone,
    string MaskedEmail,
    IReadOnlyList<OrderLineDto> Items,
    decimal SubTotal,
    decimal MemberDiscount,
    decimal VoucherDiscount,
    decimal TotalAmount,
    string? Note,
    bool CanCancel,
    DateTime CreatedAtUtc,
    Guid Id,
    OrderChannel Channel = OrderChannel.Pickup,
    string? DeliveryAddress = null,
    decimal DeliveryFee = 0,
    decimal ShippingDiscount = 0);
