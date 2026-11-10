using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Vouchers;

/// <summary>Phần thưởng ở trang quản trị: thêm trạng thái bật, thứ tự và phiên bản cho If-Match.</summary>
public sealed record AdminRedeemRewardDto(
    int Id,
    string Name,
    string? Description,
    int PointsCost,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderValue,
    int ValidDays,
    bool IsActive,
    int DisplayOrder,
    string Version);
