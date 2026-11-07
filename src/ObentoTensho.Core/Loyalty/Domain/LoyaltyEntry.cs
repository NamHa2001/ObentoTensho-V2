using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Loyalty.Domain;

// Bảng LoyaltyPoints — sổ cái điểm, chỉ thêm (DB-10). Mỗi sự kiện có mã tham chiếu chỉ ghi một lần (BR-LOY-02).
public sealed class LoyaltyEntry
{
    public long Id { get; private set; }

    public required Guid UserId { get; init; }

    public required int Points { get; init; }

    public required int BalanceAfter { get; init; }

    public required LoyaltyTransactionType Type { get; init; }

    public string? ReferenceId { get; init; }

    public string? Note { get; init; }

    public required DateTime CreatedAtUtc { get; init; }
}
