using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Auth.Domain;

public sealed class Staff
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string FullName { get; set; }

    public required string Email { get; set; }

    // NULL cho đến khi nhân viên nhận lời mời và tự đặt mật khẩu (SEC-44)
    public string? PasswordHash { get; set; }

    public required StaffRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; } = true;

    public string? InviteTokenHash { get; set; }

    public string? ResetPasswordTokenHash { get; set; }

    public DateTime? ResetPasswordExpiryUtc { get; set; }

    public DateTime? InviteExpiryUtc { get; set; }

    public required string SecurityStamp { get; set; }

    public byte[]? TotpSecretEncrypted { get; set; }

    public bool TotpEnabled { get; set; }

    // Bước TOTP đã dùng gần nhất: mã cùng bước hoặc cũ hơn bị từ chối (chống dùng lại mã vừa bị nhìn trộm)
    public long? TotpLastUsedStep { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTime? LockoutEndUtc { get; set; }

    public DateTime? PasswordChangedAtUtc { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }

    public required DateTime CreatedAtUtc { get; init; }
}
