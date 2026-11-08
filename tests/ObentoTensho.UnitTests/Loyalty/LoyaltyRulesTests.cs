using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Loyalty.Domain;

namespace ObentoTensho.UnitTests.Loyalty;

// docs/srs/13 §9: bảng hạng, không hạ hạng, công thức điểm
public sealed class LoyaltyRulesTests
{
    private static readonly ILoyaltySettings Settings = new Defaults();

    [Theory]
    [InlineData(0, MemberTier.Bronze)]
    [InlineData(299, MemberTier.Bronze)]
    [InlineData(300, MemberTier.Silver)]
    [InlineData(999, MemberTier.Silver)]
    [InlineData(1000, MemberTier.Gold)]
    [InlineData(2999, MemberTier.Gold)]
    [InlineData(3000, MemberTier.Diamond)]
    public void Hang_theo_diem_tich_luy(int accumulated, MemberTier tier) =>
        Assert.Equal(tier, LoyaltyRules.TierFor(accumulated, Settings));

    [Fact]
    public void Khong_bao_gio_ha_hang()
    {
        Assert.Equal(MemberTier.Gold, LoyaltyRules.Promote(MemberTier.Gold, 10, Settings));
        Assert.Equal(MemberTier.Diamond, LoyaltyRules.Promote(MemberTier.Silver, 3500, Settings));
    }

    [Theory]
    [InlineData(169_999, 10_000, 16)]
    [InlineData(9_999, 10_000, 0)]
    [InlineData(0, 10_000, 0)]
    [InlineData(50_000, 0, 0)]
    public void Diem_don_lam_tron_xuong(int total, int vndPerPoint, int points) =>
        Assert.Equal(points, LoyaltyRules.OrderPoints(total, vndPerPoint));

    [Fact]
    public void Giam_gia_va_hang_ke_tiep()
    {
        Assert.Equal([0, 3, 5, 8], Enum.GetValues<MemberTier>().Select(t => LoyaltyRules.DiscountPercent(t, Settings)));
        Assert.Equal((MemberTier.Gold, 1000), LoyaltyRules.Next(MemberTier.Silver, Settings));
        Assert.Null(LoyaltyRules.Next(MemberTier.Diamond, Settings));
    }

    private sealed class Defaults : ILoyaltySettings
    {
        public int VndPerPoint => 10_000;


        public int ReservationPoints => 10;

        public int ReservationPointsPerMonth => 30;

        public int PointExpiryMonths => 12;

        public int ExpiryNoticeDays => 30;

        public int AdjustApprovalThreshold => 1000;

        public int RedeemRequiredPoints => 100;

        public decimal RedeemVoucherValue => 30_000m;

        public decimal BirthdayVoucherValue => 50_000m;

        public int SilverPoints => 300;

        public int GoldPoints => 1000;

        public int DiamondPoints => 3000;

        public int SilverDiscountPercent => 3;

        public int GoldDiscountPercent => 5;

        public int DiamondDiscountPercent => 8;
    }
}
