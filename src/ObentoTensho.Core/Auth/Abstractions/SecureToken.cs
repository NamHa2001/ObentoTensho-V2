namespace ObentoTensho.Core.Auth.Abstractions;

// Value gửi cho người dùng (một lần), Hash lưu vào CSDL
public sealed record SecureToken(string Value, string Hash);
