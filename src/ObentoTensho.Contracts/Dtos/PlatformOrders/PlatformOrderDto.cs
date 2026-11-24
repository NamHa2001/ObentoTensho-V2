using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.PlatformOrders;

// Đơn app kèm doanh thu ròng và thực nhận (đơn chưa hoàn thành: tính theo tỉ lệ hiện tại để xem trước)
public sealed record PlatformOrderDto(
    Guid Id,
    string Code,
    PaymentGroup PaymentGroup,
    Platform Platform,
    string ExternalOrderCode,
    OrderStatus Status,
    DateTime CreatedAtUtc,
    int UnservedCount,
    decimal SubTotal,
    decimal? DiscountPercent,
    decimal Discount,
    decimal NetRevenue,
    decimal DeductionAmount,
    decimal VatAmount,
    decimal PitAmount,
    decimal ReceivedAmount);
