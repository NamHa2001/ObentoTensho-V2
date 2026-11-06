using Microsoft.EntityFrameworkCore;
using ObentoTensho.Infrastructure.Persistence;

namespace ObentoTensho.Infrastructure.Messaging;

// PERF-53: nhận lô bằng UPDLOCK + READPAST — nhiều worker chạy song song không lấy trùng tin, không chờ khóa của nhau.
// Tin được "thuê" tới LockedUntilUtc; worker chết giữa chừng thì hết hạn thuê, worker khác lấy lại.
internal sealed class OutboxStore(AppDbContext db)
{
    private const string Scrubbed = "{}";

    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(int batchSize, DateTime nowUtc, DateTime leaseUntilUtc, CancellationToken cancellationToken) =>
        await db.OutboxMessages.FromSql($"""
            WITH due AS (
                SELECT TOP ({batchSize}) *
                FROM [OutboxMessages] WITH (ROWLOCK, UPDLOCK, READPAST)
                WHERE [ProcessedAtUtc] IS NULL AND [FailedAtUtc] IS NULL
                  AND [NextAttemptAtUtc] <= {nowUtc}
                  AND ([LockedUntilUtc] IS NULL OR [LockedUntilUtc] < {nowUtc})
                ORDER BY [NextAttemptAtUtc], [Id]
            )
            UPDATE due SET [LockedUntilUtc] = {leaseUntilUtc}
            OUTPUT inserted.*
            """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    // Xong (đã gửi hoặc cố ý bỏ qua): xóa payload vì có thể chứa token
    public Task CompleteAsync(long id, DateTime nowUtc, CancellationToken cancellationToken) =>
        db.OutboxMessages.Where(m => m.Id == id).ExecuteUpdateAsync(
            s => s.SetProperty(m => m.ProcessedAtUtc, nowUtc).SetProperty(m => m.PayloadJson, Scrubbed).SetProperty(m => m.LockedUntilUtc, (DateTime?)null),
            cancellationToken);

    public Task RescheduleAsync(long id, DateTime nextAttemptUtc, int attempts, string? error, CancellationToken cancellationToken) =>
        db.OutboxMessages.Where(m => m.Id == id).ExecuteUpdateAsync(
            s => s
                .SetProperty(m => m.NextAttemptAtUtc, nextAttemptUtc)
                .SetProperty(m => m.Attempts, attempts)
                .SetProperty(m => m.LastError, error)
                .SetProperty(m => m.LockedUntilUtc, (DateTime?)null),
            cancellationToken);

    public Task FailAsync(long id, DateTime nowUtc, int attempts, string error, CancellationToken cancellationToken) =>
        db.OutboxMessages.Where(m => m.Id == id).ExecuteUpdateAsync(
            s => s
                .SetProperty(m => m.FailedAtUtc, nowUtc)
                .SetProperty(m => m.Attempts, attempts)
                .SetProperty(m => m.LastError, error)
                .SetProperty(m => m.PayloadJson, Scrubbed)
                .SetProperty(m => m.LockedUntilUtc, (DateTime?)null),
            cancellationToken);
}
