namespace ObentoTensho.Core.Maintenance.Abstractions;

// DB-13: mỗi hàm xóa một lô (tối đa batchSize dòng) cũ hơn mốc, trả số dòng đã xóa
public interface IDataRetentionRepository
{
    Task<int> DeleteProcessedOutboxAsync(DateTime beforeUtc, int batchSize, CancellationToken cancellationToken);

    Task<int> DeleteOtpChallengesAsync(DateTime beforeUtc, int batchSize, CancellationToken cancellationToken);

    Task<int> DeleteEmailLogsAsync(DateTime beforeUtc, int batchSize, CancellationToken cancellationToken);

    Task<int> DeleteExpiredRefreshTokensAsync(DateTime beforeUtc, int batchSize, CancellationToken cancellationToken);

    Task<int> DeleteContactMessagesAsync(DateTime beforeUtc, int batchSize, CancellationToken cancellationToken);

    // Tài khoản chưa xác thực email, chưa từng có đơn, đặt bàn, điểm, voucher, đánh giá
    Task<int> DeleteUnverifiedUsersAsync(DateTime createdBeforeUtc, int batchSize, CancellationToken cancellationToken);
}
