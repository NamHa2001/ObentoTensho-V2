namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>SEC-61: nhập lại mật khẩu hoặc mã 2FA trước thao tác rủi ro cao.</summary>
public sealed record StepUpRequest(string? Password, string? Code);
