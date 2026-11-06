using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Auth.Domain;

public sealed class RefreshToken
{
    public long Id { get; private set; }

    public required string TokenHash { get; init; }

    public required Guid FamilyId { get; init; }

    public Guid? UserId { get; init; }

    public Guid? StaffId { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }

    public string? ReplacedByTokenHash { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public RefreshTokenRevokedReason? RevokedReason { get; set; }

    public string? CreatedByIp { get; init; }

    public string? UserAgent { get; init; }

    public required DateTime CreatedAtUtc { get; init; }
}
