using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Vouchers;

/// <summary>
/// Tạo/sửa voucher (docs/srs/14). <c>Code</c> trống ⇒ sinh ngẫu nhiên 10 ký tự. Ngày là ngày Việt Nam, hiệu lực từ đầu
/// <c>StartDate</c> đến hết <c>EndDate</c>. <c>MaxUsageTotal</c> null = không giới hạn (cần người thứ hai duyệt — BR-VCH-21).
/// <c>PromotionId</c>: gắn voucher vào chương trình khuyến mãi.
/// </summary>
public sealed record VoucherRequest(
    string? Code,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderValue,
    int? MaxUsageTotal,
    int MaxUsagePerUser,
    MemberTier? ApplicableTier,
    bool RequireLogin,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsActive,
    int? PromotionId = null,
    VoucherTarget AppliesTo = VoucherTarget.Items);
