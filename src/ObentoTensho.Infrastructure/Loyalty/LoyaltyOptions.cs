using ObentoTensho.Core.Loyalty.Abstractions;

namespace ObentoTensho.Infrastructure.Loyalty;

// Giá trị mặc định theo docs/srs/19 §3 (`loyalty.*`) và D-34
public sealed class LoyaltyOptions : ILoyaltySettings
{
    public const string SectionName = "Loyalty";

    public int VndPerPoint { get; set; } = 10_000;

    public int ReservationPoints { get; set; } = 10;

    public int ReservationPointsPerMonth { get; set; } = 30;

    public int PointExpiryMonths { get; set; } = 12;

    public int ExpiryNoticeDays { get; set; } = 30;

    public int AdjustApprovalThreshold { get; set; } = 1000;

    public int RedeemRequiredPoints { get; set; } = 100;

    public decimal RedeemVoucherValue { get; set; } = 30_000m;

    public decimal BirthdayVoucherValue { get; set; } = 50_000m;

    public int SilverPoints { get; set; } = 300;

    public int GoldPoints { get; set; } = 1000;

    public int DiamondPoints { get; set; } = 3000;

    public int SilverDiscountPercent { get; set; } = 3;

    public int GoldDiscountPercent { get; set; } = 5;

    public int DiamondDiscountPercent { get; set; } = 8;

    // BR-LOY-01, BR-RPT-21: chặn cấu hình làm hỏng công thức (vd 0 đồng/điểm)
    public bool IsValid() =>
        VndPerPoint >= 1 && ReservationPoints is >= 0 and <= 1000 && ReservationPointsPerMonth is >= 0 and <= 5000
        && RedeemRequiredPoints >= 1 && RedeemVoucherValue >= 1 && BirthdayVoucherValue is >= 0 and <= 1_000_000m && PointExpiryMonths is >= 1 and <= 60 && ExpiryNoticeDays is >= 1 and <= 90 && AdjustApprovalThreshold is >= 1 and <= 100_000
        && SilverPoints >= 1 && GoldPoints > SilverPoints && DiamondPoints > GoldPoints
        && SilverDiscountPercent is >= 0 and <= 50 && GoldDiscountPercent is >= 0 and <= 50 && DiamondDiscountPercent is >= 0 and <= 50;
}
