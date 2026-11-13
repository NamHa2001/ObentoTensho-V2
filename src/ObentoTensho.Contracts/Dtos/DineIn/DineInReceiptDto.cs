using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.DineIn;

/// <summary>Phiếu thanh toán để in (không phải hóa đơn thuế).</summary>
public sealed record DineInReceiptDto(
    string Code,
    string TableName,
    int GuestCount,
    IReadOnlyList<DineInLineDto> Items,
    decimal SubTotal,
    decimal MemberDiscount,
    decimal VoucherDiscount,
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    DateTime PaidAtUtc,
    string? MemberName,
    int PointsEarned);
