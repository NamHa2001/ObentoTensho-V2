using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Reservations.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class ReservationRepository(AppDbContext db) : IReservationRepository
{
    private static readonly ReservationStatus[] Active = [ReservationStatus.Pending, ReservationStatus.Confirmed];
    private static readonly ReservationStatus[] Seated = [ReservationStatus.Pending, ReservationStatus.Confirmed, ReservationStatus.CheckedIn];

    public void Add(Reservation reservation) => db.Reservations.Add(reservation);

    public void AddHistory(ReservationStatusHistory history) => db.ReservationStatusHistory.Add(history);

    public Task<Reservation?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        db.Reservations.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Reservation?> FindByCodeForUpdateAsync(string code, CancellationToken cancellationToken) =>
        db.Reservations.SingleOrDefaultAsync(r => r.ReservationCode == code, cancellationToken);

    public async Task<IReadOnlyList<ReservationStatusHistory>> GetHistoryAsync(Guid reservationId, CancellationToken cancellationToken) =>
        await db.ReservationStatusHistory.AsNoTracking()
            .Where(h => h.ReservationId == reservationId)
            .OrderBy(h => h.Id)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Reservation> Items, int Total)> ListAsync(ReservationQuery query, CancellationToken cancellationToken)
    {
        var source = db.Reservations.AsNoTracking()
            .Where(r => (query.FromDate == null || r.ReservedDate >= query.FromDate)
                && (query.ToDate == null || r.ReservedDate <= query.ToDate)
                && (query.Status == null || r.Status == query.Status));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var code = ReservationCodes.Normalize(term);
            var phone = VietnamesePhoneNumber.TryNormalize(term, out var normalized) ? normalized : null;
            source = source.Where(r => r.ReservationCode == code || r.GuestPhone == phone || r.GuestName.Contains(term));
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderBy(r => r.ReservedAtUtc).ThenBy(r => r.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<int> CountActiveForPhoneAsync(string phone, DateTime nowUtc, CancellationToken cancellationToken) =>
        db.Reservations.CountAsync(r => r.GuestPhone == phone && Active.Contains(r.Status) && r.ReservedAtUtc >= nowUtc, cancellationToken);

    public async Task<IReadOnlySet<string>> FlaggedPhonesAsync(IReadOnlyCollection<string> phones, DateTime sinceUtc, int threshold, CancellationToken cancellationToken)
    {
        if (phones.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var flagged = await db.Reservations.AsNoTracking()
            .Where(r => phones.Contains(r.GuestPhone) && r.Status == ReservationStatus.NoShow && r.ReservedAtUtc >= sinceUtc)
            .GroupBy(r => r.GuestPhone)
            .Where(g => g.Count() >= threshold)
            .Select(g => g.Key)
            .ToListAsync(cancellationToken);
        return flagged.ToHashSet(StringComparer.Ordinal);
    }

    // DB-20: tạo dòng sức chứa nếu chưa có (khóa phạm vi để hai yêu cầu không cùng chèn), rồi tăng có điều kiện.
    // Hai lệnh nằm trong transaction của bên gọi; khóa UPDLOCK giữ tới khi commit nên các yêu cầu cùng ca xếp hàng.
    public async Task<bool> TryReserveCapacityAsync(int slotId, DateOnly date, int guests, int maxReservations, int? maxGuests, bool force, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlAsync($"""
            IF NOT EXISTS (SELECT 1 FROM [SlotDayCapacity] WITH (UPDLOCK, HOLDLOCK) WHERE [SlotId] = {slotId} AND [Date] = {date})
                INSERT INTO [SlotDayCapacity] ([SlotId], [Date], [BookedCount], [BookedGuests]) VALUES ({slotId}, {date}, 0, 0);
            """, cancellationToken);

        // Không giới hạn số khách ⇒ -1 (tránh tham số NULL không rõ kiểu)
        var guestLimit = maxGuests ?? -1;
        var rows = await db.Database.ExecuteSqlAsync($"""
            UPDATE [SlotDayCapacity]
            SET [BookedCount] = [BookedCount] + 1, [BookedGuests] = [BookedGuests] + {guests}
            WHERE [SlotId] = {slotId} AND [Date] = {date}
              AND ({force} = 1 OR ([BookedCount] + 1 <= {maxReservations} AND ({guestLimit} < 0 OR [BookedGuests] + {guests} <= {guestLimit})))
            """, cancellationToken);
        return rows == 1;
    }

    public Task ReleaseCapacityAsync(int slotId, DateOnly date, int guests, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlAsync($"""
            UPDATE [SlotDayCapacity]
            SET [BookedCount] = CASE WHEN [BookedCount] > 0 THEN [BookedCount] - 1 ELSE 0 END,
                [BookedGuests] = CASE WHEN [BookedGuests] >= {guests} THEN [BookedGuests] - {guests} ELSE 0 END
            WHERE [SlotId] = {slotId} AND [Date] = {date}
            """, cancellationToken);

    public async Task<IReadOnlyList<SlotDayCapacity>> GetCapacitiesAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken) =>
        await db.SlotDayCapacity.AsNoTracking().Where(c => c.Date >= fromDate && c.Date <= toDate).ToListAsync(cancellationToken);

    public async Task<int> NextDailySeqAsync(DateOnly date, CancellationToken cancellationToken)
    {
        const string kind = DailyCounter.ReservationKind;
        await db.Database.ExecuteSqlAsync($"""
            IF NOT EXISTS (SELECT 1 FROM [DailyCounters] WITH (UPDLOCK, HOLDLOCK) WHERE [Date] = {date} AND [Kind] = {kind})
                INSERT INTO [DailyCounters] ([Date], [Kind], [LastValue]) VALUES ({date}, {kind}, 0);
            """, cancellationToken);

        var values = await db.Database.SqlQuery<int>($"""
            UPDATE [DailyCounters] SET [LastValue] = [LastValue] + 1
            OUTPUT inserted.[LastValue] AS [Value]
            WHERE [Date] = {date} AND [Kind] = {kind}
            """).ToListAsync(cancellationToken);
        return values.Single();
    }

    public Task<bool> TableTakenAsync(int tableId, DateOnly date, int slotId, Guid exceptReservationId, CancellationToken cancellationToken) =>
        db.Reservations.AnyAsync(
            r => r.TableId == tableId && r.ReservedDate == date && r.SlotId == slotId && r.Id != exceptReservationId && Seated.Contains(r.Status),
            cancellationToken);

    public Task<bool> HasActiveForTableAsync(int tableId, CancellationToken cancellationToken) =>
        db.Reservations.AnyAsync(r => r.TableId == tableId && Seated.Contains(r.Status), cancellationToken);

    public Task<bool> HasAnyForSlotAsync(int slotId, CancellationToken cancellationToken) =>
        db.Reservations.AnyAsync(r => r.SlotId == slotId, cancellationToken);
}
