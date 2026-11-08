namespace ObentoTensho.Core.Settings.Domain;

public static class SettingKeys
{
    public const string RestaurantName = "restaurant.name";
    public const string RestaurantAddress = "restaurant.address";
    public const string RestaurantPhone = "restaurant.phone";
    public const string RestaurantEmail = "restaurant.email";
    public const string RestaurantLogo = "restaurant.logo";

    public const string PaymentBankName = "payment.bankName";
    public const string PaymentAccountNumber = "payment.accountNumber";
    public const string PaymentAccountHolderName = "payment.accountHolderName";
    public const string PaymentQrImageId = "payment.qrImageId";

    public const string ReservationMaxGuests = "reservation.maxGuestsPerBooking";
    public const string ReservationCancellationHours = "reservation.cancellationHours";
    public const string ReservationMaxAdvanceDays = "reservation.maxAdvanceDays";
    public const string ReservationMaxActivePerPhone = "reservation.maxActivePerPhone";
    public const string ReservationRequireContactVerify = "reservation.requireContactVerify";
    public const string ReservationCallAttempts = "reservation.callAttempts";
    public const string ReservationNoShowFlagCount = "reservation.noShowFlagCount";
    public const string ReservationLargePartySize = "reservation.largePartySize";
    public const string ReservationGraceMinutes = "reservation.graceMinutes";
    public const string ReservationPendingAlertMinutes = "reservation.pendingAlertMinutes";
    public const string ReservationPendingCutoffMinutes = "reservation.pendingCutoffMinutes";

    public const string OtpChannel = "otp.channel";
    public const string OtpTtlSeconds = "otp.ttlSeconds";
    public const string OtpResendSeconds = "otp.resendSeconds";
    public const string OtpMaxAttempts = "otp.maxAttempts";
    public const string OtpDailyQuota = "otp.dailyQuota";

    public const string OrderMinLeadMinutes = "order.minLeadMinutes";
    public const string OrderMaxLeadDays = "order.maxLeadDays";
    public const string OrderMaxActivePerPhone = "order.maxActivePerPhone";
    public const string OrderCallConfirmThreshold = "order.callConfirmThreshold";
    public const string OrderNoPickupMinutes = "order.noPickupMinutes";
    public const string OrderNoPickupFlagCount = "order.noPickupFlagCount";

    public const string LoyaltyPointsPerVnd = "loyalty.pointsPerVnd";
    public const string LoyaltyReservationPoints = "loyalty.reservationPoints";
    public const string LoyaltyReservationPointsPerMonth = "loyalty.reservationPointsPerMonth";
    public const string LoyaltyRedeemRequiredPoints = "loyalty.redeemRequiredPoints";
    public const string LoyaltyVoucherValue = "loyalty.voucherValue";
    public const string LoyaltyPointExpiryMonths = "loyalty.pointExpiryMonths";
    public const string LoyaltyBirthdayVoucherValue = "loyalty.birthdayVoucherValue";
    public const string LoyaltyAdjustApprovalThreshold = "loyalty.adjustApprovalThreshold";
    public const string LoyaltyClaimGuestOrders = "loyalty.claimGuestOrders";
    public const string LoyaltyTierSilver = "loyalty.tier.silver";
    public const string LoyaltyTierGold = "loyalty.tier.gold";
    public const string LoyaltyTierDiamond = "loyalty.tier.diamond";
    public const string LoyaltyTierSilverDiscount = "loyalty.tier.silverDiscount";
    public const string LoyaltyTierGoldDiscount = "loyalty.tier.goldDiscount";
    public const string LoyaltyTierDiamondDiscount = "loyalty.tier.diamondDiscount";

    public const string VoucherRedeemExpiryDays = "voucher.redeemExpiryDays";
    public const string VoucherApprovalPercent = "voucher.approvalPercent";
    public const string VoucherApprovalAmount = "voucher.approvalAmount";

    public const string ReportDayCutoffLocal = "report.dayCutoffLocal";
    public const string EmailDailyQuota = "email.dailyQuota";
    public const string I18nEnabledLocales = "i18n.enabledLocales";
}
