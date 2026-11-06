namespace ObentoTensho.Core.Audit.Domain;

// Tên hành động ổn định để lọc/cảnh báo (SEC-58); dạng <nhóm>.<đối tượng>.<sự kiện>
public static class AuditActions
{
    public const string StaffLoginSucceeded = "auth.staff.login_succeeded";
    public const string StaffLoginFailed = "auth.staff.login_failed";
    public const string StaffTwoFactorEnabled = "auth.staff.two_factor_enabled";
    public const string StaffRecoveryCodeUsed = "auth.staff.recovery_code_used";
    public const string StaffPasswordChanged = "auth.staff.password_changed";
    public const string StaffInviteAccepted = "auth.staff.invite_accepted";
    public const string RefreshTokenReuseDetected = "auth.refresh_token.reuse_detected";

    // BR-TBL-15: mọi thay đổi bàn/ca/khóa ngày/giờ mở cửa ghi trước-sau
    public const string Created = "created";
    public const string Updated = "updated";
    public const string Deleted = "deleted";
    public const string StatusChanged = "status_changed";

    public static string For(string entityType, string change) => $"tables.{entityType}.{change}";
}
