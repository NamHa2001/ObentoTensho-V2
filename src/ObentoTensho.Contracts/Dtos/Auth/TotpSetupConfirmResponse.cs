namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>Bật 2FA thành công: phiên đăng nhập và 10 mã khôi phục (chỉ hiển thị một lần).</summary>
public sealed record TotpSetupConfirmResponse(AccessTokenResponse Session, IReadOnlyList<string> RecoveryCodes);
