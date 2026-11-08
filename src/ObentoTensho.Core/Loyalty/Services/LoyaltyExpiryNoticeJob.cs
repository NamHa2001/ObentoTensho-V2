using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Loyalty.Services;

// BR-LOY-12: báo điểm sắp hết hạn — 09:00 giờ VN (docs/srs/20 §4), không gửi thư lúc nửa đêm
public sealed class LoyaltyExpiryNoticeJob(LoyaltyJobs jobs) : IDailyJob
{
    public string Name => "loyalty.expiry-notice";

    public TimeOnly RunAtLocal => new(9, 0);

    public TimeSpan Interval => TimeSpan.FromMinutes(15);

    public Task<int> RunAsync(CancellationToken cancellationToken) => jobs.NotifyExpiringAsync(cancellationToken);
}
