using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Reports.Abstractions;

// Một đơn cho báo cáo nhóm thanh toán; At = lúc hoàn thành (hoặc lúc hủy với đơn app đã hủy); các khoản null khi chưa có bản chụp
public sealed record PaymentGroupOrderRow(
    Guid Id,
    string Code,
    DateTime At,
    OrderStatus Status,
    PaymentGroup Group,
    PaymentMethod PaymentMethod,
    Platform? Platform,
    string? ExternalOrderCode,
    decimal SubTotal,
    decimal? DiscountPercent,
    decimal Discount,
    decimal? NetRevenue,
    decimal? DeductionAmount,
    decimal? VatAmount,
    decimal? PitAmount,
    decimal? ReceivedAmount);
