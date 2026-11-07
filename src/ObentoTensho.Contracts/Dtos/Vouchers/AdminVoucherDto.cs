using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Vouchers;

/// <summary>Voucher cho trang quản trị; <c>ApprovalStatus = Pending</c> ⇒ chưa dùng được tới khi người thứ hai duyệt.</summary>
public sealed record AdminVoucherDto(
    int Id,
    string Code,
    DiscountType DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscountAmount,
    decimal MinOrderValue,
    int? MaxUsageTotal,
    int MaxUsagePerUser,
    int UsedCount,
    MemberTier? ApplicableTier,
    bool RequireLogin,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsActive,
    VoucherSource Source,
    Guid? OwnerUserId,
    Guid? BatchId,
    int? PromotionId,
    ApprovalStatus ApprovalStatus,
    DateTime CreatedAtUtc,
    string Version);
