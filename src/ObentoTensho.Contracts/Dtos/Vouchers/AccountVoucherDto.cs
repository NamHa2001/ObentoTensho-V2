using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Vouchers;

/// <summary>Voucher riêng của khách (đổi điểm, sinh nhật). <c>Usable</c>: còn hạn, chưa dùng hết.</summary>
public sealed record AccountVoucherDto(
    string Code,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderValue,
    DateOnly? EndDate,
    VoucherSource Source,
    bool Usable,
    DateTime CreatedAtUtc);
