using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Vouchers;

/// <summary>
/// Một mã trong hộp chọn mã: mã riêng của khách hoặc mã gắn với khuyến mãi đang chạy.
/// <c>Usable</c> = dùng được cho giỏ này, khi đó <c>Discount</c> là số tiền được giảm; không dùng được thì <c>ErrorCode</c> cho biết lý do.
/// </summary>
public sealed record VoucherOptionDto(
    string Code,
    string? Title,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderValue,
    DateOnly? EndDate,
    VoucherSource Source,
    bool Usable,
    decimal Discount,
    string? ErrorCode,
    VoucherTarget AppliesTo = VoucherTarget.Items);
