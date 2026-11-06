using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Core.Tables.Abstractions;

// Giờ mở cửa, giờ đặc biệt, ca phục vụ, khóa ngày
public interface IScheduleRepository
{
    Task<IReadOnlyList<OpeningHour>> ListWeeklyAsync(bool forUpdate, CancellationToken cancellationToken);

    void ReplaceWeekly(IEnumerable<OpeningHour> current, IEnumerable<OpeningHour> replacement);

    Task<IReadOnlyList<SpecialHour>> ListSpecialAsync(DateOnly fromDate, DateOnly toDate, bool forUpdate, CancellationToken cancellationToken);

    void ReplaceSpecial(IEnumerable<SpecialHour> current, IEnumerable<SpecialHour> replacement);

    Task<IReadOnlyList<ServiceSlot>> ListSlotsAsync(CancellationToken cancellationToken);

    Task<ServiceSlot?> FindSlotAsync(int id, CancellationToken cancellationToken);

    void AddSlot(ServiceSlot slot);

    void RemoveSlot(ServiceSlot slot);

    Task<IReadOnlyList<BlockedDate>> ListBlockedAsync(DateOnly fromDate, CancellationToken cancellationToken);

    Task<BlockedDate?> FindBlockedAsync(int id, CancellationToken cancellationToken);

    void AddBlocked(BlockedDate blocked);

    void RemoveBlocked(BlockedDate blocked);
}
