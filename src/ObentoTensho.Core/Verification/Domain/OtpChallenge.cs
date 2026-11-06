using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Verification.Domain;

// docs/srs/08 OtpChallenges: không lưu mã rõ, không lưu email/SĐT rõ (chỉ băm); giữ 1 ngày rồi dọn (DB-13)
public sealed class OtpChallenge
{
    public long Id { get; private set; }

    public required OtpPurpose Purpose { get; init; }

    public required string TargetHash { get; init; }

    public required string CodeHash { get; init; }

    public required DateTime ExpiresAtUtc { get; set; }

    public byte Attempts { get; set; }

    public DateTime? VerifiedAtUtc { get; set; }

    public string? IpHash { get; init; }

    public Guid? RelatedEntityId { get; set; }

    public required DateTime CreatedAtUtc { get; init; }
}
