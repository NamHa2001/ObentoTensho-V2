using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Tables.Domain;
using ObentoTensho.Infrastructure.Caching;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

// Q13, PERF-51: đọc không khóa (forUpdate = false) đi qua cache lịch; bên ghi xóa cache sau khi commit
internal sealed class ScheduleRepository(AppDbContext db, IPublicScheduleCache cache, IOptions<ScheduleCacheOptions> options) : IScheduleRepository
{
    // BR-TBL-16: forUpdate giữ khóa cập nhật trên cả bảng (kể cả khi chưa có dòng nào) tới hết transaction của người gọi,
    // nên hai lần lưu đồng thời chạy lần lượt và lần sau thấy ETag đã đổi
    public async Task<IReadOnlyList<OpeningHour>> ListWeeklyAsync(bool forUpdate, CancellationToken cancellationToken)
    {
        if (!forUpdate)
        {
            return await CachedAsync("weekly", ct => db.OpeningHours.AsNoTracking().OrderBy(h => h.DayOfWeek).ThenBy(h => h.OpenTime).ToListAsync(ct), cancellationToken);
        }

        await db.Database.ExecuteSqlAsync($"SELECT COUNT(*) FROM [OpeningHours] WITH (UPDLOCK, HOLDLOCK)", cancellationToken);
        return await db.OpeningHours.OrderBy(h => h.DayOfWeek).ThenBy(h => h.OpenTime).ToListAsync(cancellationToken);
    }

    public void ReplaceWeekly(IEnumerable<OpeningHour> current, IEnumerable<OpeningHour> replacement)
    {
        db.OpeningHours.RemoveRange(current);
        db.OpeningHours.AddRange(replacement);
    }

    public async Task<IReadOnlyList<SpecialHour>> ListSpecialAsync(DateOnly fromDate, DateOnly toDate, bool forUpdate, CancellationToken cancellationToken)
    {
        if (!forUpdate)
        {
            return await CachedAsync($"special:{fromDate:O}:{toDate:O}", ct => SpecialQuery(db.SpecialHours.AsNoTracking(), fromDate, toDate).ToListAsync(ct), cancellationToken);
        }

        // Khóa khoảng ngày (HOLDLOCK) để hai người cùng đặt giờ cho một ngày chưa có dòng nào không ghi chồng
        await db.Database.ExecuteSqlAsync($"SELECT COUNT(*) FROM [SpecialHours] WITH (UPDLOCK, HOLDLOCK) WHERE [Date] BETWEEN {fromDate} AND {toDate}", cancellationToken);
        return await SpecialQuery(db.SpecialHours, fromDate, toDate).ToListAsync(cancellationToken);
    }

    public void ReplaceSpecial(IEnumerable<SpecialHour> current, IEnumerable<SpecialHour> replacement)
    {
        db.SpecialHours.RemoveRange(current);
        db.SpecialHours.AddRange(replacement);
    }

    public Task<IReadOnlyList<ServiceSlot>> ListSlotsAsync(CancellationToken cancellationToken) =>
        CachedAsync("slots", ct => db.ServiceSlots.AsNoTracking().ToListAsync(ct), cancellationToken);

    public Task<ServiceSlot?> FindSlotAsync(int id, CancellationToken cancellationToken) =>
        db.ServiceSlots.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public void AddSlot(ServiceSlot slot) => db.ServiceSlots.Add(slot);

    public void RemoveSlot(ServiceSlot slot) => db.ServiceSlots.Remove(slot);

    public Task<IReadOnlyList<BlockedDate>> ListBlockedAsync(DateOnly fromDate, CancellationToken cancellationToken) =>
        CachedAsync(
            string.Create(CultureInfo.InvariantCulture, $"blocked:{fromDate:O}"),
            ct => db.BlockedDates.AsNoTracking().Where(b => b.Date >= fromDate).OrderBy(b => b.Date).ThenBy(b => b.StartTime).ToListAsync(ct),
            cancellationToken);

    public Task<BlockedDate?> FindBlockedAsync(int id, CancellationToken cancellationToken) =>
        db.BlockedDates.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

    public void AddBlocked(BlockedDate blocked) => db.BlockedDates.Add(blocked);

    public void RemoveBlocked(BlockedDate blocked) => db.BlockedDates.Remove(blocked);

    private static IQueryable<SpecialHour> SpecialQuery(IQueryable<SpecialHour> source, DateOnly fromDate, DateOnly toDate) =>
        source.Where(h => h.Date >= fromDate && h.Date <= toDate).OrderBy(h => h.Date).ThenBy(h => h.OpenTime);

    private async Task<IReadOnlyList<T>> CachedAsync<T>(string key, Func<CancellationToken, Task<List<T>>> load, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return await load(cancellationToken);
        }

        return await cache.GetOrCreateAsync<IReadOnlyList<T>>("rows:" + key, async ct => (await load(ct)).AsReadOnly(), cancellationToken);
    }
}
