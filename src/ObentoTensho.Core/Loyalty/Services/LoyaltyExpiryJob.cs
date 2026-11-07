using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Loyalty.Services;

// docs/srs/13 §4.4: hết hạn điểm — chạy mỗi giờ (lô hết hạn theo thời điểm cộng, không chỉ lúc 01:00)
public sealed class LoyaltyExpiryJob(LoyaltyJobs jobs) : IRecurringJob
{
    public string Name => "loyalty.expire";

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public Task<int> RunAsync(CancellationToken cancellationToken) => jobs.ExpirePointsAsync(cancellationToken);
}
