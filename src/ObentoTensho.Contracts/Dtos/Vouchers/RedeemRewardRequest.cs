using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Vouchers;

/// <summary>Tạo/sửa phần thưởng đổi điểm. Mức giảm theo cùng quy tắc voucher (BR-VCH-01); <c>ValidDays</c>: voucher nhận được còn hạn bấy nhiêu ngày.</summary>
public sealed record RedeemRewardRequest(
    string Name,
    string? Description,
    int PointsCost,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderValue,
    int ValidDays,
    bool IsActive);
