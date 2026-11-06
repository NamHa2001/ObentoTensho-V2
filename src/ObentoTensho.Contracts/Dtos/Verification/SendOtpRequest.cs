using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Verification;

/// <summary>Gửi mã xác minh 6 số qua email (kênh mặc định — `otp.channel`). Cần Turnstile.</summary>
public sealed record SendOtpRequest(OtpPurpose Purpose, string Email, string? Lang);
