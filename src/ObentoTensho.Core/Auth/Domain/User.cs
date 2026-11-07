using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Auth.Domain;

public sealed class User
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string FullName { get; set; }

    public required string Email { get; set; }

    public required string EmailCanonical { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsPhoneVerified { get; set; }

    public string? PasswordHash { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public DateTime? DateOfBirthSetAtUtc { get; set; }

    public byte DateOfBirthEditCount { get; set; }

    public string? PendingEmail { get; set; }

    public string? PendingEmailTokenHash { get; set; }

    public DateTime? PendingEmailExpiryUtc { get; set; }

    public MemberTier MemberTier { get; set; } = MemberTier.Bronze;

    public int CurrentPoints { get; set; }

    public int PointsDebt { get; set; }

    public int AccumulatedPoints { get; set; }

    public bool IsEmailVerified { get; set; }

    public string? EmailVerifyTokenHash { get; set; }

    public DateTime? EmailVerifyExpiryUtc { get; set; }

    public string? ResetPasswordTokenHash { get; set; }

    public DateTime? ResetPasswordExpiryUtc { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTime? LockoutEndUtc { get; set; }

    public DateTime? PasswordChangedAtUtc { get; set; }

    public required string SecurityStamp { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    // BR-CUS-05: tới mốc này job ẩn danh hóa; trước đó khách hủy được
    public DateTime? DeletionScheduledAtUtc { get; set; }

    public string PreferredLang { get; set; } = "vi";

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; private set; } = [];
}
