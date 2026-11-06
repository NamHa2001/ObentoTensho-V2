namespace ObentoTensho.Contracts.Enums;

public enum RefreshTokenRevokedReason
{
    Rotated,
    Logout,
    ReuseDetected,
    PasswordChanged,
    AccountLocked
}
