using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Reservations.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Reservations.Services;

// Ba việc định kỳ của đặt bàn (docs/srs/20 §4). Mỗi đặt bàn xử lý trong transaction riêng và kiểm lại trạng thái
// trên bản mới nhất (RowVersion) nên chạy trùng hay chạy lại không gửi thư/đổi trạng thái hai lần.
public sealed class ReservationJobs(
    IReservationRepository reservations,
    ReservationTransitions transitions,
    IReservationEmailOutbox emails,
    IReservationSettings settings,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const int BatchSize = 200;

    // Cửa sổ quét quanh mốc nhắc (job chạy mỗi 15 phút): giờ hẹn ∈ (now + lead − 15′, now + lead + 15′]
    private static readonly TimeSpan ReminderWindow = TimeSpan.FromMinutes(15);

    // Không quét ngược quá xa để danh sách nhỏ
    private static readonly TimeSpan LookBack = TimeSpan.FromDays(2);

    // BR-RSV-08: nhắc đúng một lần, theo giờ hẹn thật (UTC suy từ giờ VN)
    public async Task<int> SendRemindersAsync(CancellationToken cancellationToken)
    {
        var target = clock.UtcNow.AddMinutes(settings.ReminderLeadMinutes);
        var ids = await reservations.ListIdsAsync(ReservationStatus.Confirmed, target - ReminderWindow, target + ReminderWindow, reminderNotSent: true, BatchSize, cancellationToken);
        var sent = 0;
        foreach (var id in ids)
        {
            var result = await unitOfWork.TryInTransactionAsync(async ct =>
            {
                if (await reservations.FindForUpdateAsync(id, ct) is not { Status: ReservationStatus.Confirmed, ReminderSentAtUtc: null } reservation)
                {
                    return Result.Failure<bool>("skipped");
                }

                reservation.ReminderSentAtUtc = clock.UtcNow;
                emails.EnqueueReminder(reservation);
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Success(true);
            }, cancellationToken);
            sent += result.IsSuccess ? 1 : 0;
        }

        return sent;
    }

    // BR-RSV-24: quá giờ hẹn + graceMinutes chưa check-in ⇒ No-show, trả chỗ (khách đến muộn: lễ tân chuyển 5 → 2)
    public Task<int> MarkNoShowsAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.UtcNow.AddMinutes(-settings.GraceMinutes);
        return TransitionAllAsync(ReservationStatus.Confirmed, cutoff - LookBack, cutoff, ReservationStatus.NoShow, reason: null, cancellationToken);
    }

    // BR-RSV-25: còn ≤ pendingCutoffMinutes tới giờ hẹn mà chưa ai xử lý ⇒ tự hủy, trả chỗ, xin lỗi khách
    public Task<int> CancelStalePendingAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.UtcNow.AddMinutes(settings.PendingCutoffMinutes);
        return TransitionAllAsync(ReservationStatus.Pending, cutoff - LookBack, cutoff, ReservationStatus.Cancelled, "Quán chưa kịp xác nhận đặt bàn", cancellationToken);
    }

    private async Task<int> TransitionAllAsync(ReservationStatus from, DateTime fromUtc, DateTime toUtc, ReservationStatus to, string? reason, CancellationToken cancellationToken)
    {
        var ids = await reservations.ListIdsAsync(from, fromUtc, toUtc, reminderNotSent: false, BatchSize, cancellationToken);
        var changed = 0;
        foreach (var id in ids)
        {
            var result = await unitOfWork.TryInTransactionAsync(async ct =>
            {
                if (await reservations.FindForUpdateAsync(id, ct) is not { } reservation || reservation.Status != from)
                {
                    return Result.Failure<bool>("skipped");
                }

                var applied = await transitions.ApplyAsync(reservation, to, ReservationActor.System, actorId: null, reason, ct);
                if (!applied.IsSuccess)
                {
                    return Result.Failure<bool>(applied.ErrorCode ?? "skipped");
                }

                await unitOfWork.SaveChangesAsync(ct);
                return Result.Success(true);
            }, cancellationToken);
            changed += result.IsSuccess ? 1 : 0;
        }

        return changed;
    }
}
