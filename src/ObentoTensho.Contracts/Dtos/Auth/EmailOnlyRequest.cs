namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>Quên mật khẩu / gửi lại thư xác thực: phản hồi luôn trung tính (BR-AUTH-14).</summary>
public sealed record EmailOnlyRequest(string Email);
