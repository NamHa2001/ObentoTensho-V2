namespace ObentoTensho.Core.Auth.Domain;

// Bỏ bước 2FA khi đăng nhập quản trị để thử giao diện. Program.cs chỉ bật được ở môi trường Development
public sealed record StaffTwoFactorPolicy(bool SkipOnSignIn);
