using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Loyalty.Services;

// BR-LOY-12: báo điểm sắp hết hạn — mỗi 6 giờ
public sealed class LoyaltyExpiryNoticeJob(LoyaltyJobs jobs) : IRecurringJob
{
    public string Name => "loyalty.expiry-notice";

    public TimeSpan Interval => TimeSpan.FromHours(6);

    public Task<int> RunAsync(CancellationToken cancellationToken) => jobs.NotifyExpiringAsync(cancellationToken);
}
