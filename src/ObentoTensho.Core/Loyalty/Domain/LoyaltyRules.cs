using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Loyalty.Abstractions;

namespace ObentoTensho.Core.Loyalty.Domain;

// docs/srs/13 §5: công thức điểm, hạng theo điểm tích lũy (không bao giờ hạ hạng), giảm giá theo hạng
public static class LoyaltyRules
{
    // BR-LOY-01: điểm = ⌊tổng tiền sau giảm / số đồng mỗi điểm⌋
    public static int OrderPoints(decimal totalAmount, int vndPerPoint) =>
        vndPerPoint <= 0 || totalAmount <= 0 ? 0 : (int)decimal.Floor(totalAmount / vndPerPoint);

    // BR-LOY-03
    public static MemberTier TierFor(int accumulatedPoints, ILoyaltySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return accumulatedPoints switch
        {
            _ when accumulatedPoints >= settings.DiamondPoints => MemberTier.Diamond,
            _ when accumulatedPoints >= settings.GoldPoints => MemberTier.Gold,
            _ when accumulatedPoints >= settings.SilverPoints => MemberTier.Silver,
            _ => MemberTier.Bronze,
        };
    }

    public static MemberTier Promote(MemberTier current, int accumulatedPoints, ILoyaltySettings settings) =>
        (MemberTier)Math.Max((byte)current, (byte)TierFor(accumulatedPoints, settings));

    public static int DiscountPercent(MemberTier tier, ILoyaltySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return tier switch
        {
            MemberTier.Diamond => settings.DiamondDiscountPercent,
            MemberTier.Gold => settings.GoldDiscountPercent,
            MemberTier.Silver => settings.SilverDiscountPercent,
            _ => 0,
        };
    }

    public static (MemberTier Tier, int Points)? Next(MemberTier tier, ILoyaltySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return tier switch
        {
            MemberTier.Bronze => (MemberTier.Silver, settings.SilverPoints),
            MemberTier.Silver => (MemberTier.Gold, settings.GoldPoints),
            MemberTier.Gold => (MemberTier.Diamond, settings.DiamondPoints),
            _ => null,
        };
    }
}
