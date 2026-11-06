namespace ObentoTensho.Core.Notifications.Domain;

// Tên loại email lưu ở EmailLogs.Type và dùng làm tên tệp mẫu
public static class EmailTypes
{
    public static string Of(EmailKind kind) => kind switch
    {
        EmailKind.VerifyEmail => "verify_email",
        EmailKind.DuplicateRegistration => "duplicate_registration",
        EmailKind.PasswordReset => "password_reset",
        EmailKind.PasswordChanged => "password_changed",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Loại email chưa có tên."),
    };

    public static IReadOnlyList<string> SecurityNotices { get; } =
        Enum.GetValues<EmailKind>().Where(EmailPolicy.IsSecurityNotice).Select(Of).ToArray();
}
