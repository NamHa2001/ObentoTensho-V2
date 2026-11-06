namespace ObentoTensho.Core.Auth.Abstractions;

// Thông tin thiết bị lưu kèm phiên cho trang "Thiết bị đăng nhập" (FR-AUTH-09)
public sealed record ClientInfo(string? IpAddress, string? UserAgent);
