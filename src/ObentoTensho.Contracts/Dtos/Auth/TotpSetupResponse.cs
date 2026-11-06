namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>Khóa TOTP để quét mã QR (<see cref="OtpAuthUri"/>) hoặc nhập tay (<see cref="Secret"/>).</summary>
public sealed record TotpSetupResponse(string Secret, string OtpAuthUri);
