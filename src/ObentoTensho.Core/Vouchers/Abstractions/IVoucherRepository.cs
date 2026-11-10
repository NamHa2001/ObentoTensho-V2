using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Core.Vouchers.Abstractions;

public interface IVoucherRepository
{
    Task<Voucher?> FindByCodeAsync(string code, CancellationToken cancellationToken);

    // Bản được theo dõi để sửa
    Task<Voucher?> FindAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Voucher>> FindBatchAsync(Guid batchId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Voucher> Items, int Total)> ListAsync(VoucherQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<Voucher>> ListOwnedAsync(Guid userId, CancellationToken cancellationToken);

    // Mã chung gắn với khuyến mãi đang chạy hôm nay, kèm tên khuyến mãi để khách nhận ra
    Task<IReadOnlyList<(Voucher Voucher, string Title)>> ListPromotedAsync(DateOnly today, CancellationToken cancellationToken);

    // Danh mục phần thưởng đổi điểm, theo thứ tự hiển thị
    Task<IReadOnlyList<RedeemReward>> ListRewardsAsync(bool activeOnly, CancellationToken cancellationToken);

    // Bản được theo dõi để sửa
    Task<RedeemReward?> FindRewardAsync(int id, CancellationToken cancellationToken);

    void AddReward(RedeemReward reward);

    void RemoveReward(RedeemReward reward);

    // Xuất Excel: cùng bộ lọc với danh sách, không phân trang, có trần
    Task<IReadOnlyList<Voucher>> ListForExportAsync(VoucherQuery query, int limit, CancellationToken cancellationToken);

    // BR-LOY-09/20: khách có sinh nhật ngày này, email đã xác thực, chưa có voucher sinh nhật năm nay; đọc theo lô, sau afterId
    Task<IReadOnlyList<User>> ListBirthdayDueAsync(int month, int day, bool includeLeapDay, int year, Guid? afterId, int limit, CancellationToken cancellationToken);

    Task<bool> PromotionExistsAsync(int promotionId, CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> ExistingCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken);

    Task<int> UsedByUserAsync(int voucherId, Guid userId, CancellationToken cancellationToken);

    // BR-VCH-11: đã có người dùng hoặc đơn tham chiếu ⇒ không xóa được
    Task<bool> HasUsageAsync(int voucherId, CancellationToken cancellationToken);

    void Add(Voucher voucher);

    void AddRange(IEnumerable<Voucher> vouchers);

    void Remove(Voucher voucher);

    // BR-VCH-07, DB-21: tăng lượt dùng có điều kiện trong một lệnh UPDATE; false = hết lượt/không còn hiệu lực
    Task<bool> TryUseAsync(int voucherId, CancellationToken cancellationToken);

    Task<bool> TryUseForUserAsync(int voucherId, Guid userId, int maxPerUser, DateTime nowUtc, CancellationToken cancellationToken);

    // BR-ORD-07: hoàn lượt khi hủy đơn (không xuống dưới 0)
    Task ReleaseAsync(int voucherId, Guid? userId, CancellationToken cancellationToken);
}
