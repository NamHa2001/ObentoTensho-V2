using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Orders.Services;

// docs/srs/20 #30, BR-ORD-18 — mỗi 10 phút
public sealed class OrderAttentionJob(OrderJobs jobs) : IRecurringJob
{
    public string Name => "orders.attention";

    public TimeSpan Interval => TimeSpan.FromMinutes(10);

    public Task<int> RunAsync(CancellationToken cancellationToken) => jobs.NotifyAttentionAsync(cancellationToken);
}
