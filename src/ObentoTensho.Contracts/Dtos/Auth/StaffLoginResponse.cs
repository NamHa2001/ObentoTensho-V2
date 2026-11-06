namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>
/// Kết quả đăng nhập nhân viên: hoặc có <see cref="Session"/> (đã vào được),
/// hoặc có <see cref="Challenge"/> (cần làm tiếp bước được chỉ định bằng mã tạm).
/// </summary>
public sealed record StaffLoginResponse(AccessTokenResponse? Session, StaffChallengeResponse? Challenge);
