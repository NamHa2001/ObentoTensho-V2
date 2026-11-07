namespace ObentoTensho.Core.Vouchers.Domain;

// Bộ đếm lượt dùng voucher theo khách (UNIQUE VoucherId + UserId); chỉ tăng/giảm bằng UPDATE có điều kiện
public sealed class UserVoucher
{
    public long Id { get; private set; }

    public required Guid UserId { get; init; }

    public required int VoucherId { get; init; }

    public int UsedCount { get; private set; }

    public DateTime? LastUsedAtUtc { get; private set; }

    public required DateTime CreatedAtUtc { get; init; }
}
