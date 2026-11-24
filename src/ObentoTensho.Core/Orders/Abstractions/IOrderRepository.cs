using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Orders.Domain;

namespace ObentoTensho.Core.Orders.Abstractions;

public interface IOrderRepository
{
    void Add(Order order);

    void AddHistory(OrderStatusHistory history);

    // Bản được theo dõi để sửa; withItems = kèm dòng món và tùy chọn
    Task<Order?> FindForUpdateAsync(Guid id, bool withItems, CancellationToken cancellationToken);

    Task<Order?> FindByCodeAsync(string code, bool withItems, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderStatusHistory>> GetHistoryAsync(Guid orderId, CancellationToken cancellationToken);

    // Kèm dòng món; ngày lọc là ngày lấy theo giờ VN
    Task<(IReadOnlyList<Order> Items, int Total)> ListAsync(OrderQuery query, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);

    // Màn hình bếp: đơn đang mở, sắp theo giờ lấy, kèm dòng món
    Task<IReadOnlyList<Order>> ListOpenAsync(int limit, CancellationToken cancellationToken);

    // Tại bàn / mang về tại quầy: phiếu đang mở (được theo dõi, kèm món và tùy chọn)
    Task<Order?> FindOpenDineInAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> TableHasOpenBillAsync(int tableId, CancellationToken cancellationToken);

    // docs/srs/23 BR-PLT-06: mã đơn app đã có ở đơn chưa hủy cùng nhóm, kênh con
    Task<bool> ExternalCodeTakenAsync(PaymentGroup group, Platform platform, string code, Guid? exceptOrderId, CancellationToken cancellationToken);

    // Đơn app (mọi trạng thái), theo dõi thay đổi, kèm món
    Task<Order?> FindPlatformOrderAsync(Guid id, CancellationToken cancellationToken);

    // Tại bàn: mọi phiếu đang mở, kèm món (sơ đồ gọi món, màn hình bếp)
    Task<IReadOnlyList<Order>> ListOpenDineInAsync(CancellationToken cancellationToken);

    Task<(IReadOnlyList<Order> Items, int Total)> ListForUserAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);

    // BR-ORD-20: đơn đang xử lý của một SĐT
    Task<int> CountOpenForPhoneAsync(string phone, CancellationToken cancellationToken);

    // BR-ORD-21: SĐT đã từng có đơn hoàn thành chưa
    Task<bool> HasCompletedForPhoneAsync(string phone, CancellationToken cancellationToken);

    // DB-16: số thứ tự trong ngày VN, tăng nguyên tử
    Task<int> NextDailySeqAsync(DateOnly date, CancellationToken cancellationToken);

    // BR-ORD-18: SĐT nào trong danh sách có ≥ threshold đơn hủy vì không đến lấy kể từ sinceUtc
    Task<IReadOnlySet<string>> FlaggedPhonesAsync(IReadOnlyCollection<string> phones, DateTime sinceUtc, int threshold, CancellationToken cancellationToken);

    // docs/srs/20 #30: đơn có thể cần xử lý (lọc thô ở CSDL, phân loại ở OrderAttention), kèm dòng món
    Task<IReadOnlyList<Order>> ListAttentionAsync(DateTime pendingCreatedBeforeUtc, DateTime pendingPickupBeforeUtc, DateTime readyPickupBeforeUtc, int limit, CancellationToken cancellationToken);

    Task MarkAttentionNotifiedAsync(IReadOnlyCollection<Guid> ids, DateTime nowUtc, CancellationToken cancellationToken);

    // BR-ORD-09: Unpaid → Paid có điều kiện trong một lệnh UPDATE (hai thu ngân bấm cùng lúc chỉ một lần ghi)
    Task<bool> TryMarkPaidAsync(Guid id, Guid staffId, DateTime nowUtc, CancellationToken cancellationToken);
}
