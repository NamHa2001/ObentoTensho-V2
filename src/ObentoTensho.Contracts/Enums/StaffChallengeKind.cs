namespace ObentoTensho.Contracts.Enums;

// Bước còn thiếu sau khi nhân viên nhập đúng mật khẩu (docs/srs/10 §4.2)
public enum StaffChallengeKind
{
    PasswordChange,
    TotpVerify,
    TotpSetup,

    // SEC-61: không phải bước đăng nhập, là bằng chứng vừa xác thực lại
    StepUp
}
