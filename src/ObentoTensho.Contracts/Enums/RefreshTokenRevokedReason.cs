namespace ObentoTensho.Contracts.Enums;

public enum RefreshTokenRevokedReason
{
    Rotated,
    Logout,
    ReuseDetected,
    PasswordChanged,
    AccountLocked,

    // SEC-52: vượt số phiên tối đa, phiên cũ nhất bị thu hồi
    SessionLimit
}
