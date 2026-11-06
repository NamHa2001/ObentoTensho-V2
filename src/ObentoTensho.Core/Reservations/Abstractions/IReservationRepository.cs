using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Reservations.Domain;

namespace ObentoTensho.Core.Reservations.Abstractions;

public interface IReservationRepository
{
    void Add(Reservation reservation);

    void AddHistory(ReservationStatusHistory history);

    // Bản được theo dõi để sửa
    Task<Reservation?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<Reservation?> FindByCodeForUpdateAsync(string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReservationStatusHistory>> GetHistoryAsync(Guid reservationId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Reservation> Items, int Total)> ListAsync(ReservationQuery query, CancellationToken cancellationToken);

    // BR-RSV-20: đặt bàn còn hoạt động (Pending/Confirmed, chưa tới giờ) của một SĐT
    Task<int> CountActiveForPhoneAsync(string phone, DateTime nowUtc, CancellationToken cancellationToken);

    // BR-RSV-22: SĐT nào trong danh sách có ≥ `threshold` lần không đến kể từ `sinceUtc`
    Task<IReadOnlySet<string>> FlaggedPhonesAsync(IReadOnlyCollection<string> phones, DateTime sinceUtc, int threshold, CancellationToken cancellationToken);

    // DB-20: giữ chỗ nguyên tử; false khi ca đã đầy (số lượt hoặc số khách). force = vượt có chủ đích (Manager+)
    Task<bool> TryReserveCapacityAsync(int slotId, DateOnly date, int guests, int maxReservations, int? maxGuests, bool force, CancellationToken cancellationToken);

    Task ReleaseCapacityAsync(int slotId, DateOnly date, int guests, CancellationToken cancellationToken);

    Task<IReadOnlyList<SlotDayCapacity>> GetCapacitiesAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);

    // DB-16: số thứ tự trong ngày VN, tăng nguyên tử
    Task<int> NextDailySeqAsync(DateOnly date, CancellationToken cancellationToken);

    // BR-TBL-17: một bàn chỉ phục vụ một đặt bàn đang hoạt động trong cùng ngày và cùng ca
    Task<bool> TableTakenAsync(int tableId, DateOnly date, int slotId, Guid exceptReservationId, CancellationToken cancellationToken);

    // BR-TBL-06: bàn còn đặt bàn chưa kết thúc thì không xóa được
    Task<bool> HasActiveForTableAsync(int tableId, CancellationToken cancellationToken);

    // Ca đã từng có đặt bàn thì chỉ được tắt, không xóa (lịch sử đặt bàn tham chiếu ca)
    Task<bool> HasAnyForSlotAsync(int slotId, CancellationToken cancellationToken);

    // Cho các việc theo lịch; mỗi lần lấy giới hạn số lượng để một lượt chạy không quá lâu
    Task<IReadOnlyList<Guid>> ListIdsAsync(ReservationStatus status, DateTime fromUtc, DateTime toUtc, bool reminderNotSent, int limit, CancellationToken cancellationToken);
}
