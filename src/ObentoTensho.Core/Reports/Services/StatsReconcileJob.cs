using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Reports.Services;

// BR-RPT-05: đối soát DailyStats 03:30 giờ VN (docs/srs/20 §4)
public sealed class StatsReconcileJob(StatsRollupService rollup) : IDailyJob
{
    public string Name => "reports.stats-reconcile";

    public TimeOnly RunAtLocal => new(3, 30);

    public TimeSpan Interval => TimeSpan.FromMinutes(15);

    public Task<int> RunAsync(CancellationToken cancellationToken) => rollup.ReconcileAsync(cancellationToken);
}
