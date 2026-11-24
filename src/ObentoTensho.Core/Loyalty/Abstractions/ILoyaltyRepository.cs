using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Loyalty.Domain;

namespace ObentoTensho.Core.Loyalty.Abstractions;

public interface ILoyaltyRepository
{
    // Khóa hàng Users (UPDLOCK) trong transaction hiện tại rồi trả bản được theo dõi: mọi thao tác điểm của một khách chạy tuần tự
    Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken);

    // BR-LOY-04: khách có số dư khác tổng sổ cái hoặc khác tổng điểm còn lại của các lô (chỉ đọc)
    Task<IReadOnlyList<Guid>> FindBalanceMismatchesAsync(int limit, CancellationToken cancellationToken);

    Task<User?> FindUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> EntryExistsAsync(Guid userId, LoyaltyTransactionType type, string referenceId, CancellationToken cancellationToken);

    Task<LoyaltyEntry?> FindEntryAsync(Guid userId, LoyaltyTransactionType type, string referenceId, CancellationToken cancellationToken);

    // Tổng điểm một loại kể từ mốc (trần điểm check-in theo tháng — BR-LOY-22)
    Task<int> SumPointsAsync(Guid userId, LoyaltyTransactionType type, DateTime sinceUtc, CancellationToken cancellationToken);

    void AddEntry(LoyaltyEntry entry);

    void AddLot(LoyaltyLot lot);

    // Lô còn điểm của khách, hạn sớm nhất trước (bản được theo dõi để trừ)
    Task<IReadOnlyList<LoyaltyLot>> ListOpenLotsAsync(Guid userId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<LoyaltyEntry> Items, int Total)> ListEntriesAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);

    // Điểm sắp hết hạn trong khoảng (để hiển thị)
    Task<(int Points, DateTime? NextExpiryUtc)> ExpiringAsync(Guid userId, DateTime untilUtc, CancellationToken cancellationToken);

    // Cho việc định kỳ: khách có lô đã hết hạn / sắp hết hạn chưa báo
    Task<IReadOnlyList<Guid>> UsersWithExpiredLotsAsync(DateTime nowUtc, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> UsersToNotifyExpiryAsync(DateTime nowUtc, DateTime untilUtc, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<LoyaltyLot>> ListLotsToNotifyAsync(Guid userId, DateTime nowUtc, DateTime untilUtc, CancellationToken cancellationToken);

    void AddAdjustment(LoyaltyAdjustment adjustment);

    Task<LoyaltyAdjustment?> FindAdjustmentAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<LoyaltyAdjustment>> ListAdjustmentsAsync(ApprovalStatus? status, int limit, CancellationToken cancellationToken);

    /// <summary>Tên hiển thị của nhiều khách trong một lần truy vấn (cho danh sách đề nghị điều chỉnh).</summary>
    Task<IReadOnlyDictionary<Guid, string>> UserNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
