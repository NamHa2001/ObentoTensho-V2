using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class ScheduleRepository(AppDbContext db) : IScheduleRepository
{
    // BR-TBL-16: forUpdate giữ khóa cập nhật trên cả bảng (kể cả khi chưa có dòng nào) tới hết transaction của người gọi,
    // nên hai lần lưu đồng thời chạy lần lượt và lần sau thấy ETag đã đổi
    public async Task<IReadOnlyList<OpeningHour>> ListWeeklyAsync(bool forUpdate, CancellationToken cancellationToken)
    {
        if (forUpdate)
        {
            await db.Database.ExecuteSqlAsync($"SELECT COUNT(*) FROM [OpeningHours] WITH (UPDLOCK, HOLDLOCK)", cancellationToken);
        }

        return await Track(db.OpeningHours, forUpdate).OrderBy(h => h.DayOfWeek).ThenBy(h => h.OpenTime).ToListAsync(cancellationToken);
    }

    public void ReplaceWeekly(IEnumerable<OpeningHour> current, IEnumerable<OpeningHour> replacement)
    {
        db.OpeningHours.RemoveRange(current);
        db.OpeningHours.AddRange(replacement);
    }

    public async Task<IReadOnlyList<SpecialHour>> ListSpecialAsync(DateOnly fromDate, DateOnly toDate, bool forUpdate, CancellationToken cancellationToken)
    {
        if (forUpdate)
        {
            // Khóa khoảng ngày (HOLDLOCK) để hai người cùng đặt giờ cho một ngày chưa có dòng nào không ghi chồng
            await db.Database.ExecuteSqlAsync($"SELECT COUNT(*) FROM [SpecialHours] WITH (UPDLOCK, HOLDLOCK) WHERE [Date] BETWEEN {fromDate} AND {toDate}", cancellationToken);
        }

        return await Track(db.SpecialHours, forUpdate)
            .Where(h => h.Date >= fromDate && h.Date <= toDate)
            .OrderBy(h => h.Date).ThenBy(h => h.OpenTime)
            .ToListAsync(cancellationToken);
    }

    public void ReplaceSpecial(IEnumerable<SpecialHour> current, IEnumerable<SpecialHour> replacement)
    {
        db.SpecialHours.RemoveRange(current);
        db.SpecialHours.AddRange(replacement);
    }

    public async Task<IReadOnlyList<ServiceSlot>> ListSlotsAsync(CancellationToken cancellationToken) =>
        await db.ServiceSlots.AsNoTracking().ToListAsync(cancellationToken);

    public Task<ServiceSlot?> FindSlotAsync(int id, CancellationToken cancellationToken) =>
        db.ServiceSlots.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public void AddSlot(ServiceSlot slot) => db.ServiceSlots.Add(slot);

    public void RemoveSlot(ServiceSlot slot) => db.ServiceSlots.Remove(slot);

    public async Task<IReadOnlyList<BlockedDate>> ListBlockedAsync(DateOnly fromDate, CancellationToken cancellationToken) =>
        await db.BlockedDates.AsNoTracking()
            .Where(b => b.Date >= fromDate)
            .OrderBy(b => b.Date).ThenBy(b => b.StartTime)
            .ToListAsync(cancellationToken);

    public Task<BlockedDate?> FindBlockedAsync(int id, CancellationToken cancellationToken) =>
        db.BlockedDates.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

    public void AddBlocked(BlockedDate blocked) => db.BlockedDates.Add(blocked);

    public void RemoveBlocked(BlockedDate blocked) => db.BlockedDates.Remove(blocked);

    private static IQueryable<T> Track<T>(DbSet<T> set, bool forUpdate)
        where T : class => forUpdate ? set : set.AsNoTracking();
}
