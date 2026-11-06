namespace ObentoTensho.Core.Verification.Domain;

// BR-AUTH-18 (`otp.*` — docs/srs/19 §3): mã 6 số, hạn 5 phút, sai 5 lần hủy mã, gửi lại cách ≥ 60 giây, ≤ 3 mã/giờ/đích
public static class OtpPolicy
{
    public const int CodeLength = 6;
    public const int MaxAttempts = 5;
    public const int MaxPerHourPerTarget = 3;

    // Một IP (vd bot) không gửi được mã tới hàng loạt địa chỉ khác nhau
    public const int MaxPerHourPerIp = 10;

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ResendInterval = TimeSpan.FromSeconds(60);
}
