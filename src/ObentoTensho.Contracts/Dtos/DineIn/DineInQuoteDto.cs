namespace ObentoTensho.Contracts.Dtos.DineIn;

/// <summary>Tổng tiền xem trước khi thanh toán.</summary>
public sealed record DineInQuoteDto(decimal SubTotal, decimal MemberDiscount, decimal VoucherDiscount, decimal TotalAmount, string? MemberName);
