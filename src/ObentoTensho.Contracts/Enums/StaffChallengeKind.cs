namespace ObentoTensho.Contracts.Enums;

// Bước còn thiếu sau khi nhân viên nhập đúng mật khẩu (docs/srs/10 §4.2)
public enum StaffChallengeKind
{
    PasswordChange,
    TotpVerify,
    TotpSetup
}
