using ObentoTensho.Core.Common;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Reservations.Services;

// BR-RSV-09: đầu ngày (00:05 giờ VN) bàn của các đặt bàn đã xác nhận hôm nay chuyển "Đã đặt";
// bàn đang "Dọn dẹp" hoặc "Có khách" giữ nguyên (cùng luật SyncTableAsync)
public sealed class ReservationTableDayStartJob(
    IReservationRepository reservations,
    ReservationTransitions transitions,
    IUnitOfWork unitOfWork,
    IClock clock) : IDailyJob
{
    public string Name => "reservations.table-day-start";

    public TimeOnly RunAtLocal => new(0, 5);

    public TimeSpan Interval => TimeSpan.FromMinutes(15);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var today = await reservations.ListConfirmedWithTableAsync(clock.TodayLocal, cancellationToken);
        foreach (var reservation in today)
        {
            await transitions.SyncTableAsync(reservation, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return today.Count;
    }
}
