namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>Đặt lại mật khẩu bằng token trong email (FR-AUTH-05).</summary>
public sealed record ResetPasswordRequest(string Token, string NewPassword);
