using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Vouchers.Domain;

// BR-LOY-05/07: danh mục phần thưởng đổi điểm do quán đặt; đổi một lần tạo một voucher riêng theo đúng điều kiện ghi ở đây
public sealed class RedeemReward
{
    public int Id { get; private set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public int PointsCost { get; set; }

    public DiscountType DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal? MaxDiscountAmount { get; set; }

    public decimal MinOrderValue { get; set; }

    public int ValidDays { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; private set; } = [];
}
