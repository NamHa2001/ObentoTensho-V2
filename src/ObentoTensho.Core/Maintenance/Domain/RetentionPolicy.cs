namespace ObentoTensho.Core.Maintenance.Domain;

// DB-13: thời hạn giữ dữ liệu. AuditLogs giữ ≥ 12 tháng nên không dọn ở đây. Thời hạn chốt lại theo ý kiến pháp lý (Q6)
public static class RetentionPolicy
{
    public static readonly TimeSpan ProcessedOutbox = TimeSpan.FromDays(7);
    public static readonly TimeSpan OtpChallenges = TimeSpan.FromDays(1);
    public static readonly TimeSpan EmailLogs = TimeSpan.FromDays(90);
    public static readonly TimeSpan ExpiredRefreshTokens = TimeSpan.FromDays(30);
    public static readonly TimeSpan ContactMessages = TimeSpan.FromDays(730);
    public static readonly TimeSpan UnverifiedAccounts = TimeSpan.FromDays(30);

    public const int BatchSize = 1000;

    // Mỗi lượt xóa tối đa chừng này lô mỗi loại; còn thì để lượt sau, tránh giữ CSDL bận quá lâu
    public const int MaxBatchesPerRun = 50;
}
