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
