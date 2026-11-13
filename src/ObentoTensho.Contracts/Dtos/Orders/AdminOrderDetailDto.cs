namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Chi tiết đơn cho thu ngân/quản lý: dòng món (tiếng Việt), tiền, lịch sử, lý do hủy, hoàn tiền.</summary>
public sealed record AdminOrderDetailDto(
    AdminOrderDto Order,
    IReadOnlyList<OrderLineDto> Items,
    decimal SubTotal,
    decimal MemberDiscount,
    decimal VoucherDiscount,
    string? Note,
    string? CancelReason,
    DateTime? PaidAtUtc,
    DateTime? RefundedAtUtc,
    string? RefundNote,
    IReadOnlyList<OrderHistoryDto> History,
    string? DeliveryAddress = null,
    decimal? DeliveryDistanceKm = null,
    decimal DeliveryFee = 0);
