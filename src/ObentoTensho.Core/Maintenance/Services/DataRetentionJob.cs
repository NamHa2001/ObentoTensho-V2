using ObentoTensho.Core.Common;
using ObentoTensho.Core.Maintenance.Abstractions;
using ObentoTensho.Core.Maintenance.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Maintenance.Services;

// DB-13, docs/srs/20 §4: dọn dữ liệu theo vòng đời lúc 02:00 giờ VN, theo lô nhỏ để không khóa bảng
public sealed class DataRetentionJob(IDataRetentionRepository data, IClock clock) : IDailyJob
{
    public string Name => "maintenance.data-retention";

    public TimeOnly RunAtLocal => new(2, 0);

    public TimeSpan Interval => TimeSpan.FromMinutes(15);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var deleted = 0;
        deleted += await DrainAsync((size, ct) => data.DeleteProcessedOutboxAsync(now - RetentionPolicy.ProcessedOutbox, size, ct), cancellationToken);
        deleted += await DrainAsync((size, ct) => data.DeleteOtpChallengesAsync(now - RetentionPolicy.OtpChallenges, size, ct), cancellationToken);
        deleted += await DrainAsync((size, ct) => data.DeleteEmailLogsAsync(now - RetentionPolicy.EmailLogs, size, ct), cancellationToken);
        deleted += await DrainAsync((size, ct) => data.DeleteExpiredRefreshTokensAsync(now - RetentionPolicy.ExpiredRefreshTokens, size, ct), cancellationToken);
        deleted += await DrainAsync((size, ct) => data.DeleteContactMessagesAsync(now - RetentionPolicy.ContactMessages, size, ct), cancellationToken);
        deleted += await DrainAsync((size, ct) => data.DeleteUnverifiedUsersAsync(now - RetentionPolicy.UnverifiedAccounts, size, ct), cancellationToken);
        return deleted;
    }

    private static async Task<int> DrainAsync(Func<int, CancellationToken, Task<int>> deleteBatch, CancellationToken cancellationToken)
    {
        var total = 0;
        for (var batch = 0; batch < RetentionPolicy.MaxBatchesPerRun; batch++)
        {
            var deleted = await deleteBatch(RetentionPolicy.BatchSize, cancellationToken);
            total += deleted;
            if (deleted < RetentionPolicy.BatchSize)
            {
                break;
            }
        }

        return total;
    }
}
