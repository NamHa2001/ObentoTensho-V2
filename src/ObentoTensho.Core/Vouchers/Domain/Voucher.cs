using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Vouchers.Domain;

// docs/srs/08 §6 Vouchers. Hiệu lực [StartUtc, EndUtc) — EndUtc là đầu ngày sau EndDate (giờ VN), tức hết hạn cuối ngày (BR-VCH-02).
public sealed class Voucher
{
    public int Id { get; private set; }

    public int? PromotionId { get; set; }

    public required string Code { get; init; }

    public DiscountType DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public decimal MinOrderValue { get; set; }

    public int? MaxUsageTotal { get; set; }

    public int MaxUsagePerUser { get; set; } = 1;

    public int UsedCount { get; private set; }

    public MemberTier? ApplicableTier { get; set; }

    public Guid? OwnerUserId { get; init; }

    public bool RequireLogin { get; set; }

    public VoucherSource Source { get; init; }

    public DateTime? StartUtc { get; set; }

    public DateTime? EndUtc { get; set; }

    public bool IsActive { get; set; } = true;

    // BR-VCH-21: voucher giá trị cao / sinh hàng loạt lớn chờ người thứ hai duyệt
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;

    public Guid? CreatedByStaffId { get; init; }

    public Guid? ReviewedByStaffId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public Guid? BatchId { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; private set; } = [];
}
