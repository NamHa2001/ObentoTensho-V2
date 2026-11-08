namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>Mã TOTP 6 số từ ứng dụng xác thực.</summary>
public sealed record TotpCodeRequest(string Code);
