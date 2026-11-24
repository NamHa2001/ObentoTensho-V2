using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Reports;

/// <summary>Một đơn trong bảng chi tiết: đơn hoàn thành theo ngày hoàn thành; đơn app đã hủy giữ dòng, các khoản bằng 0.</summary>
public sealed record PaymentGroupOrderDto(
    Guid Id,
    string Code,
    DateTime AtUtc,
    OrderStatus Status,
    PaymentGroup Group,
    PaymentMethod PaymentMethod,
    Platform? Platform,
    string? ExternalOrderCode,
    decimal SubTotal,
    decimal? DiscountPercent,
    decimal Discount,
    decimal NetRevenue,
    decimal DeductionAmount,
    decimal VatAmount,
    decimal PitAmount,
    decimal ReceivedAmount);
