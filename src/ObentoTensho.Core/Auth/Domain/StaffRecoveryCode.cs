namespace ObentoTensho.Core.Auth.Domain;

// docs/srs/08: mã khôi phục 2FA dùng một lần, chỉ lưu băm
public sealed class StaffRecoveryCode
{
    public long Id { get; private set; }

    public required Guid StaffId { get; init; }

    public required string CodeHash { get; init; }

    public DateTime? UsedAtUtc { get; set; }
}
