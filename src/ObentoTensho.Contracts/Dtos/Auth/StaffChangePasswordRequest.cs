namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>Đổi mật khẩu bắt buộc ngay sau đăng nhập (mã tạm loại password_change).</summary>
public sealed record StaffChangePasswordRequest(string ChallengeToken, string CurrentPassword, string NewPassword);
