using System.Collections.Frozen;
using ObentoTensho.Contracts.Enums;
using static ObentoTensho.Core.Settings.Domain.SettingKeys;

namespace ObentoTensho.Core.Settings.Domain;

// docs/srs/19 §3.1: danh mục duy nhất; khóa không có ở đây không được tạo
public static class SettingsCatalog
{
    private const decimal IntMax = int.MaxValue;

    public static readonly IReadOnlyList<SettingDefinition> All =
    [
        Text(RestaurantName, "Obento Tensho", "Tên quán", isPublic: true),
        Text(RestaurantAddress, string.Empty, "Địa chỉ quán", isPublic: true),
        new(RestaurantPhone, SettingType.Text, string.Empty, "Điện thoại quán", Protected: true, IsPublic: true),
        new(RestaurantEmail, SettingType.Email, string.Empty, "Email quán", Protected: true, IsPublic: true),
        new(RestaurantLogo, SettingType.Image, string.Empty, "Logo quán", IsPublic: true),

        new("social.facebook", SettingType.Url, string.Empty, "Facebook", IsPublic: true),
        new("social.zalo", SettingType.Url, string.Empty, "Zalo", IsPublic: true),
        new("social.instagram", SettingType.Url, string.Empty, "Instagram", IsPublic: true),
        new("social.tiktok", SettingType.Url, string.Empty, "TikTok", IsPublic: true),
        new("social.youtube", SettingType.Url, string.Empty, "YouTube", IsPublic: true),

        new(PaymentBankName, SettingType.Text, string.Empty, "Ngân hàng nhận chuyển khoản", Protected: true, IsPublic: true),
        new(PaymentAccountNumber, SettingType.Text, string.Empty, "Số tài khoản", Protected: true, IsPublic: true),
        new(PaymentAccountHolderName, SettingType.Text, string.Empty, "Chủ tài khoản", Protected: true, IsPublic: true),
        new(PaymentQrImageId, SettingType.Image, string.Empty, "Ảnh QR chuyển khoản", Protected: true, IsPublic: true),

        Int(ReservationMaxGuests, 20, 1, 100, "Số khách tối đa mỗi lượt đặt"),
        Int(ReservationCancellationHours, 0, 0, 72, "Hạn hủy trước giờ hẹn (giờ); 0 = hủy tự do"),
        Int(ReservationMaxAdvanceDays, 60, 1, 365, "Đặt trước tối đa (ngày)"),
        Int(ReservationMaxActivePerPhone, 2, 1, 10, "Số đặt bàn đang mở mỗi SĐT"),
        Flag(ReservationRequireContactVerify, false, "Chế độ chống lạm dụng: mọi khách phải OTP"),
        Int(ReservationCallAttempts, 3, 1, 10, "Số lần gọi xác nhận"),
        Int(ReservationNoShowFlagCount, 2, 1, 10, "Số lần không đến thì gắn cờ SĐT"),
        Int(ReservationLargePartySize, 8, 2, 50, "Từ số khách này phải gọi xác nhận"),
        Int(ReservationGraceMinutes, 15, 5, 120, "Quá giờ hẹn bấy nhiêu phút thì tính không đến"),
        Int(ReservationPendingAlertMinutes, 30, 5, 240, "Chờ xác nhận quá bấy nhiêu phút thì nhắc nhân viên"),
        Int(ReservationPendingCutoffMinutes, 60, 5, 240, "Còn bấy nhiêu phút tới giờ hẹn mà vẫn chờ thì tự hủy"),

        new(OtpChannel, SettingType.Choice, "email", "Kênh gửi OTP", Choices: ["email", "sms", "zalo"]),
        new(OtpRequirePhoneVerify, SettingType.Bool, "true", "Bắt buộc xác minh SĐT bằng OTP SMS trước khi đặt món, đặt bàn (tắt: chỉ cần email đã xác minh)", IsPublic: true),
        Int(OtpTtlSeconds, 300, 60, 900, "Hiệu lực mã OTP (giây)"),
        Int(OtpResendSeconds, 60, 30, 600, "Chờ trước khi gửi lại mã (giây)"),
        Int(OtpMaxAttempts, 5, 3, 10, "Số lần nhập sai tối đa"),
        Int(OtpDailyQuota, 200, 10, 100_000, "Số mã OTP mỗi ngày"),

        Int(OrderMinLeadMinutes, 30, 10, 240, "Giờ lấy sớm nhất sau lúc đặt (phút)"),
        Int(OrderMaxLeadDays, 7, 1, 30, "Đặt trước tối đa (ngày)"),
        Int(OrderMaxActivePerPhone, 3, 1, 10, "Số đơn đang mở mỗi SĐT"),
        Int(OrderCallConfirmThreshold, 500_000, 0, 10_000_000, "Đơn tiền mặt từ mức này phải gọi xác nhận (₫)"),
        Int(OrderNoPickupMinutes, 60, 15, 240, "Sẵn sàng quá bấy nhiêu phút sau giờ lấy thì báo không đến lấy"),
        Int(OrderNoPickupFlagCount, 2, 1, 10, "Số lần không đến lấy thì gắn cờ SĐT"),

        Int(LoyaltyPointsPerVnd, 10_000, 1, IntMax, "Số đồng cho 1 điểm"),
        Int(LoyaltyReservationPoints, 10, 0, 1000, "Điểm mỗi lần check-in đặt bàn"),
        Int(LoyaltyReservationPointsPerMonth, 30, 0, 5000, "Trần điểm đặt bàn mỗi tháng"),
        Int(LoyaltyPointExpiryMonths, 12, 1, 60, "Điểm hết hạn sau (tháng)"),
        Int(LoyaltyBirthdayVoucherValue, 50_000, 0, 1_000_000, "Giá trị voucher sinh nhật (₫); 0 = tắt"),
        Int(LoyaltyAdjustApprovalThreshold, 1000, 1, 100_000, "Điều chỉnh từ mức này cần người khác duyệt"),
        Int(LoyaltyTierSilver, 300, 1, IntMax, "Điểm tích lũy lên hạng Bạc"),
        Int(LoyaltyTierGold, 1000, 1, IntMax, "Điểm tích lũy lên hạng Vàng"),
        Int(LoyaltyTierDiamond, 3000, 1, IntMax, "Điểm tích lũy lên hạng Kim cương"),
        Int(LoyaltyTierSilverDiscount, 3, 0, 50, "% giảm hạng Bạc"),
        Int(LoyaltyTierGoldDiscount, 5, 0, 50, "% giảm hạng Vàng"),
        Int(LoyaltyTierDiamondDiscount, 8, 0, 50, "% giảm hạng Kim cương"),

        Int(VoucherRedeemExpiryDays, 30, 1, 365, "Voucher sinh nhật còn hạn (ngày)"),
        Int(VoucherApprovalPercent, 50, 1, 100, "Giảm từ mức % này cần duyệt"),
        Int(VoucherApprovalAmount, 200_000, 1, IntMax, "Giảm từ mức tiền này cần duyệt (₫)"),

        new(ReportDayCutoffLocal, SettingType.Time, "00:00", "Mốc chốt ngày báo cáo", Max: 6),
        Int(EmailDailyQuota, 300, 10, 100_000, "Hạn mức thư mỗi ngày của gói gửi thư"),

        new("i18n.enabledLocales", SettingType.List, "vi", "Ngôn ngữ đang bật cho khách", Choices: ["vi", "en", "ja"], IsPublic: true),
        new("i18n.defaultLocale", SettingType.Choice, "vi", "Ngôn ngữ gốc", Choices: ["vi"], IsPublic: true),

        Text("security.turnstileSiteKey", string.Empty, "Khóa site Cloudflare Turnstile (công khai)", isPublic: true),
        Text("seo.defaultTitle", string.Empty, "Tiêu đề SEO mặc định", isPublic: true),
        Text("seo.defaultDescription", string.Empty, "Mô tả SEO mặc định", isPublic: true),
        new("seo.defaultOgImageId", SettingType.Image, string.Empty, "Ảnh chia sẻ mặc định", IsPublic: true),
    ];

    private static readonly FrozenDictionary<string, SettingDefinition> ByKey = All.ToFrozenDictionary(d => d.Key, StringComparer.Ordinal);

    public static SettingDefinition? Find(string key) => ByKey.GetValueOrDefault(key);

    private static SettingDefinition Int(string key, int value, decimal min, decimal max, string description) =>
        new(key, SettingType.Number, value.ToString(System.Globalization.CultureInfo.InvariantCulture), description, min, max);

    private static SettingDefinition Flag(string key, bool value, string description) =>
        new(key, SettingType.Bool, value ? "true" : "false", description);

    private static SettingDefinition Text(string key, string value, string description, bool isPublic) =>
        new(key, SettingType.Text, value, description, IsPublic: isPublic);
}
