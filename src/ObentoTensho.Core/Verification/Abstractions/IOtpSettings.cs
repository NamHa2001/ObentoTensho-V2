namespace ObentoTensho.Core.Verification.Abstractions;

// `otp.dailyQuota` (docs/srs/19 §3): tổng số mã mỗi ngày VN — chặn kẻ xấu đốt hạn mức email
public interface IOtpSettings
{
    int DailyQuota { get; }

    int TtlSeconds { get; }

    int ResendSeconds { get; }

    int MaxAttempts { get; }
}
