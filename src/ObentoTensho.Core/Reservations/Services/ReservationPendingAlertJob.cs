using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Tables.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Reservations.Services;

// BR-RSV-25: đặt bàn chờ xác nhận quá reservation.pendingAlertMinutes (trong giờ mở cửa) ⇒ một thư tóm tắt cho lễ tân và quản lý,
// mỗi đặt bàn chỉ nhắc một lần; tới giờ cắt thì ReservationPendingCutoffJob tự hủy. Mỗi 5 phút
public sealed class ReservationPendingAlertJob(
    IReservationRepository reservations,
    IScheduleRepository schedule,
    IStaffRepository staff,
    IAdminEmailOutbox adminEmails,
    IReservationSettings settings,
    IUnitOfWork unitOfWork,
    IClock clock) : IRecurringJob
{
    private const int BatchSize = 50;

    public string Name => "reservations.pending-alert";

    public TimeSpan Interval => TimeSpan.FromMinutes(5);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (!await IsOpenNowAsync(cancellationToken))
        {
            return 0;
        }

        var overdue = await reservations.ListPendingNotAlertedAsync(now.AddMinutes(-settings.PendingAlertMinutes), BatchSize, cancellationToken);
        if (overdue.Count == 0)
        {
            return 0;
        }

        var codes = string.Join(", ", overdue.Select(r => $"{r.ReservationCode} ({r.ReservedDate:dd/MM} {r.ReservedTime:HH\\:mm})"));
        var message = $"Có {overdue.Count} đặt bàn chờ xác nhận quá {settings.PendingAlertMinutes} phút: {codes}. Hãy gọi xác minh và xác nhận hoặc từ chối.";
        var key = $"reservation-pending:{now.Ticks}";
        foreach (var email in await staff.ListActiveEmailsAsync(StaffCapabilities.RolesFor(StaffCapability.Reservations), cancellationToken))
        {
            adminEmails.EnqueueAlert(email, message, key);
        }

        foreach (var reservation in overdue)
        {
            reservation.PendingAlertedAtUtc = now;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return overdue.Count;
    }

    // "Trong giờ mở cửa" theo lịch tuần và giờ đặc biệt của hôm nay (giờ VN)
    private async Task<bool> IsOpenNowAsync(CancellationToken cancellationToken)
    {
        var today = clock.TodayLocal;
        var nowLocal = TimeOnly.FromDateTime(clock.ToLocal(clock.UtcNow));
        var opening = new OpeningSchedule(
            await schedule.ListWeeklyAsync(forUpdate: false, cancellationToken),
            await schedule.ListSpecialAsync(today.AddDays(-1), today, forUpdate: false, cancellationToken));
        return opening.IsOpen(today, nowLocal, nowLocal.AddMinutes(1));
    }
}
