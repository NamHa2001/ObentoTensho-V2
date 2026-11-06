namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>Bước 2FA: gửi mã TOTP 6 số hoặc một mã khôi phục.</summary>
public sealed record TwoFactorVerifyRequest(string ChallengeToken, string? Code, string? RecoveryCode);
