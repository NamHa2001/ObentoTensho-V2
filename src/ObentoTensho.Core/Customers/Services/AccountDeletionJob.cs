using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Customers.Services;

// BR-CUS-05: ẩn danh hóa tài khoản đã hết 7 ngày chờ — 04:00 giờ VN (docs/srs/20 §4)
public sealed class AccountDeletionJob(AccountDeletionService deletion) : IDailyJob
{
    public string Name => "customers.account-deletion";

    public TimeOnly RunAtLocal => new(4, 0);

    public TimeSpan Interval => TimeSpan.FromMinutes(15);

    public Task<int> RunAsync(CancellationToken cancellationToken) => deletion.AnonymizeDueAsync(cancellationToken);
}
