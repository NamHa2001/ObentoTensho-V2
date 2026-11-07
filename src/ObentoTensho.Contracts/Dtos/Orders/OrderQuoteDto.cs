namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Giá giỏ theo CSDL lúc này. <c>IsValid</c> = mọi dòng đặt được. Giảm giá hạng/voucher có ở phần điểm và voucher.</summary>
public sealed record OrderQuoteDto(
    bool IsValid,
    IReadOnlyList<QuoteLineDto> Lines,
    decimal SubTotal,
    decimal MemberDiscount,
    decimal VoucherDiscount,
    decimal TotalAmount);
