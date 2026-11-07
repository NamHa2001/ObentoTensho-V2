using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Reports.Services;

public sealed class StatsRollupJob(StatsRollupService rollup) : IRecurringJob
{
    public string Name => "reports.stats-rollup";

    public TimeSpan Interval => TimeSpan.FromMinutes(5);

    public Task<int> RunAsync(CancellationToken cancellationToken) => rollup.RefreshRecentAsync(cancellationToken);
}
