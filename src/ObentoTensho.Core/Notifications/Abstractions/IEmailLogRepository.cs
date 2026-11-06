using ObentoTensho.Core.Notifications.Domain;

namespace ObentoTensho.Core.Notifications.Abstractions;

public interface IEmailLogRepository
{
    Task<EmailLog?> FindForUpdateAsync(string idempotencyKey, CancellationToken cancellationToken);

    void Add(EmailLog log);

    // Đếm thư đã gửi tới một địa chỉ từ mốc `sinceUtc`; types null = mọi loại
    Task<int> CountSentToAsync(string toEmail, IReadOnlyCollection<string>? types, DateTime sinceUtc, CancellationToken cancellationToken);

    Task<int> CountSentSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken);
}
