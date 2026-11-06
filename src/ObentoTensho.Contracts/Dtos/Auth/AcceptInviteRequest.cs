namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>Nhân viên mới tự đặt mật khẩu từ liên kết mời (SEC-44).</summary>
public sealed record AcceptInviteRequest(string Token, string Password);
