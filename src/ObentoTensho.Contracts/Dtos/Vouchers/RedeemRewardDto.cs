using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Vouchers;

/// <summary>Phần thưởng khách đổi được bằng điểm.</summary>
public sealed record RedeemRewardDto(
    int Id,
    string Name,
    string? Description,
    int PointsCost,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderValue,
    int ValidDays);
