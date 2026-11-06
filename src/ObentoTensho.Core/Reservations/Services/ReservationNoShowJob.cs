using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Reservations.Services;

// BR-RSV-24 — mỗi 5 phút
public sealed class ReservationNoShowJob(ReservationJobs jobs) : IRecurringJob
{
    public string Name => "reservations.no-show";

    public TimeSpan Interval => TimeSpan.FromMinutes(5);

    public Task<int> RunAsync(CancellationToken cancellationToken) => jobs.MarkNoShowsAsync(cancellationToken);
}
