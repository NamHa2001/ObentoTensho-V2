using Microsoft.Extensions.Options;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Infrastructure.Loyalty;
using static ObentoTensho.Core.Settings.Domain.SettingKeys;

namespace ObentoTensho.Infrastructure.Settings;

internal sealed class StoreLoyaltySettings(ISettingsReader s, IOptions<LoyaltyOptions> options) : ILoyaltySettings
{
    public int VndPerPoint => s.GetInt(LoyaltyPointsPerVnd);


    public int ReservationPoints => s.GetInt(LoyaltyReservationPoints);

    public int ReservationPointsPerMonth => s.GetInt(LoyaltyReservationPointsPerMonth);

    public int PointExpiryMonths => s.GetInt(LoyaltyPointExpiryMonths);

    public int ExpiryNoticeDays => options.Value.ExpiryNoticeDays;

    public int AdjustApprovalThreshold => s.GetInt(LoyaltyAdjustApprovalThreshold);

    public decimal BirthdayVoucherValue => s.GetInt(LoyaltyBirthdayVoucherValue);

    public int SilverPoints => s.GetInt(LoyaltyTierSilver);

    public int GoldPoints => s.GetInt(LoyaltyTierGold);

    public int DiamondPoints => s.GetInt(LoyaltyTierDiamond);

    public int SilverDiscountPercent => s.GetInt(LoyaltyTierSilverDiscount);

    public int GoldDiscountPercent => s.GetInt(LoyaltyTierGoldDiscount);

    public int DiamondDiscountPercent => s.GetInt(LoyaltyTierDiamondDiscount);
}
