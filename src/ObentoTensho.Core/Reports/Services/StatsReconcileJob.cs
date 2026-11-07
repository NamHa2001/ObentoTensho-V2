using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Reports.Services;

public sealed class StatsReconcileJob(StatsRollupService rollup) : IRecurringJob
{
    public string Name => "reports.stats-reconcile";

    public TimeSpan Interval => TimeSpan.FromDays(1);

    public Task<int> RunAsync(CancellationToken cancellationToken) => rollup.ReconcileAsync(cancellationToken);
}
