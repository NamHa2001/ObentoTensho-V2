using Microsoft.EntityFrameworkCore;
using ObentoTensho.Core.Maintenance.Abstractions;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

// Mỗi lệnh là một DELETE TOP(n) riêng, không mở transaction dài
internal sealed class DataRetentionRepository(AppDbContext db) : IDataRetentionRepository
{
    // Thư lỗi hẳn giữ 30 ngày để còn điều tra
    private static readonly TimeSpan FailedOutboxExtra = TimeSpan.FromDays(23);

    public Task<int> DeleteProcessedOutboxAsync(DateTime beforeUtc, int batchSize, CancellationToken cancellationToken) =>
        db.OutboxMessages
            .Where(m => (m.ProcessedAtUtc != null && m.ProcessedAtUtc < beforeUtc) || (m.FailedAtUtc != null && m.FailedAtUtc < beforeUtc - FailedOutboxExtra))
            .OrderBy(m => m.Id)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteOtpChallengesAsync(DateTime beforeUtc, int batchSize, CancellationToken cancellationToken) =>
        db.OtpChallenges.Where(c => c.CreatedAtUtc < beforeUtc).OrderBy(c => c.CreatedAtUtc).Take(batchSize).ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteEmailLogsAsync(DateTime beforeUtc, int batchSize, CancellationToken cancellationToken) =>
        db.EmailLogs.Where(l => l.CreatedAtUtc < beforeUtc).OrderBy(l => l.CreatedAtUtc).Take(batchSize).ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteExpiredRefreshTokensAsync(DateTime beforeUtc, int batchSize, CancellationToken cancellationToken) =>
        db.RefreshTokens.Where(t => t.ExpiresAtUtc < beforeUtc).OrderBy(t => t.Id).Take(batchSize).ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteContactMessagesAsync(DateTime beforeUtc, int batchSize, CancellationToken cancellationToken) =>
        db.ContactMessages.Where(m => m.CreatedAtUtc < beforeUtc).OrderBy(m => m.Id).Take(batchSize).ExecuteDeleteAsync(cancellationToken);

    public async Task<int> DeleteUnverifiedUsersAsync(DateTime createdBeforeUtc, int batchSize, CancellationToken cancellationToken)
    {
        var ids = await db.Users.AsNoTracking()
            .Where(u => !u.IsEmailVerified && !u.IsDeleted && u.CreatedAtUtc < createdBeforeUtc
                && !db.Orders.Any(o => o.UserId == u.Id)
                && !db.Reservations.Any(r => r.UserId == u.Id)
                && !db.LoyaltyPoints.Any(p => p.UserId == u.Id)
                && !db.LoyaltyLots.Any(l => l.UserId == u.Id)
                && !db.LoyaltyAdjustments.Any(a => a.UserId == u.Id)
                && !db.Vouchers.Any(v => v.OwnerUserId == u.Id)
                && !db.UserVouchers.Any(v => v.UserId == u.Id)
                && !db.Reviews.Any(r => r.UserId == u.Id))
            .OrderBy(u => u.CreatedAtUtc)
            .Select(u => u.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
        if (ids.Count == 0)
        {
            return 0;
        }

        await db.RefreshTokens.Where(t => t.UserId != null && ids.Contains(t.UserId.Value)).ExecuteDeleteAsync(cancellationToken);
        await db.ConsentRecords.Where(c => c.UserId != null && ids.Contains(c.UserId.Value)).ExecuteDeleteAsync(cancellationToken);
        return await db.Users.Where(u => ids.Contains(u.Id)).ExecuteDeleteAsync(cancellationToken);
    }
}
