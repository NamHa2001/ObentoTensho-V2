using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Reservations.Services;

// BR-RSV-25 — mỗi 5 phút
public sealed class ReservationPendingCutoffJob(ReservationJobs jobs) : IRecurringJob
{
    public string Name => "reservations.pending-cutoff";

    public TimeSpan Interval => TimeSpan.FromMinutes(5);

    public Task<int> RunAsync(CancellationToken cancellationToken) => jobs.CancelStalePendingAsync(cancellationToken);
}
