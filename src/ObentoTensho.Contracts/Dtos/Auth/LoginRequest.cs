namespace ObentoTensho.Contracts.Dtos.Auth;

/// <summary>Đăng nhập bằng email và mật khẩu (FR-AUTH-02).</summary>
public sealed record LoginRequest(string Email, string Password);
