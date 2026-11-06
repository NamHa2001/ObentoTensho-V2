using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Reservations.Services;

// BR-RSV-08 — mỗi 15 phút
public sealed class ReservationReminderJob(ReservationJobs jobs) : IRecurringJob
{
    public string Name => "reservations.reminder";

    public TimeSpan Interval => TimeSpan.FromMinutes(15);

    public Task<int> RunAsync(CancellationToken cancellationToken) => jobs.SendRemindersAsync(cancellationToken);
}
