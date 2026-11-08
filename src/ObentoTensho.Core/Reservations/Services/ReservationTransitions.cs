using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Loyalty.Services;
using ObentoTensho.Core.Realtime.Abstractions;
using ObentoTensho.Core.Realtime.Domain;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Reservations.Domain;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Reservations.Services;

// Áp một chuyển trạng thái lên đặt bàn đang được theo dõi, trong transaction của bên gọi:
// kiểm máy trạng thái, trả chỗ (BR-RSV-04), đồng bộ trạng thái bàn (BR-RSV-09), lịch sử (BR-RSV-11), thư cho khách
public sealed class ReservationTransitions(
    IReservationRepository reservations,
    ITableLayoutRepository tables,
    LoyaltyLedger ledger,
    ILoyaltyRepository loyaltyRepository,
    ILoyaltySettings loyaltySettings,
    IReservationEmailOutbox emails,
    IRealtimeEvents realtime,
    IClock clock)
{
    public async Task<Result> ApplyAsync(Reservation reservation, ReservationStatus to, ReservationActor actor, Guid? actorId, string? reason, CancellationToken cancellationToken)
    {
        if (CheckAllowed(reservation, to, actor, reason) is { } failure)
        {
            return failure;
        }

        var from = reservation.Status;
        var now = clock.UtcNow;
        reservation.Status = to;
        reservation.UpdatedAtUtc = now;
        if (to == ReservationStatus.Cancelled)
        {
            reservation.CancelReason = reason?.Trim();
            reservation.CancelledAtUtc = now;
            reservation.CancelledByType = actor;
            reservation.CancelledById = actorId;
        }

        if (ReservationStateMachine.ReleasesCapacity(to))
        {
            await reservations.ReleaseCapacityAsync(reservation.SlotId, reservation.ReservedDate, reservation.GuestCount, cancellationToken);
            realtime.Stage(RealtimeMessages.Availability(reservation));
        }

        await SyncTableAsync(reservation, cancellationToken);
        if (to == ReservationStatus.CheckedIn)
        {
            await EarnCheckInPointsAsync(reservation, cancellationToken);
        }

        reservations.AddHistory(new ReservationStatusHistory
        {
            ReservationId = reservation.Id,
            FromStatus = from,
            ToStatus = to,
            ChangedByType = actor,
            ChangedById = actorId,
            Reason = reason?.Trim(),
            ChangedAtUtc = now,
        });
        Notify(reservation, from, to, actor, reason);
        realtime.Stage(RealtimeMessages.ReservationChanged(reservation));
        return Result.Success();
    }

    // BR-RSV-09: 0–2 tự động theo đặt bàn trong ngày VN; "Dọn dẹp" do nhân viên đặt nên không ghi đè
    public async Task SyncTableAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        if (reservation.TableId is not { } tableId || await tables.FindTableAsync(tableId, cancellationToken) is not { } table
            || table.CurrentStatus == TableStatus.Cleaning)
        {
            return;
        }

        var today = reservation.ReservedDate == clock.TodayLocal;
        var before = table.CurrentStatus;
        table.CurrentStatus = reservation.Status switch
        {
            ReservationStatus.CheckedIn => TableStatus.Occupied,
            ReservationStatus.Confirmed when today => TableStatus.Reserved,
            _ when table.CurrentStatus is TableStatus.Reserved or TableStatus.Occupied => TableStatus.Available,
            _ => table.CurrentStatus,
        };
        if (table.CurrentStatus != before)
        {
            realtime.Stage(RealtimeMessages.TableChanged(table));
        }
    }

    // BR-RSV-07, BR-LOY-22: thành viên check-in được cộng điểm một lần cho mỗi đặt bàn, tối đa trần mỗi tháng (giờ VN);
    // hết trần vẫn check-in bình thường
    private async Task EarnCheckInPointsAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        if (reservation.UserId is not { } userId || reservation.PointsEarned > 0)
        {
            return;
        }

        var today = clock.TodayLocal;
        var monthStart = clock.ToUtc(new DateOnly(today.Year, today.Month, 1), TimeOnly.MinValue);
        var used = await loyaltyRepository.SumPointsAsync(userId, LoyaltyTransactionType.ReservationEarn, monthStart, cancellationToken);
        var points = Math.Min(loyaltySettings.ReservationPoints, loyaltySettings.ReservationPointsPerMonth - used);
        reservation.PointsEarned = await ledger.EarnAsync(userId, LoyaltyTransactionType.ReservationEarn, reservation.ReservationCode, points, note: null, cancellationToken);
    }

    private Result? CheckAllowed(Reservation reservation, ReservationStatus to, ReservationActor actor, string? reason)
    {
        var from = reservation.Status;
        if (!ReservationStateMachine.CanTransition(from, to, actor))
        {
            return Result.Failure(ErrorCodes.InvalidTransition);
        }

        if (ReservationStateMachine.RequiresReason(from, to, actor) && string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["reason"] = [FieldErrorCodes.Required],
            });
        }

        // Khôi phục no-show chỉ trong ngày đặt (khách đến muộn)
        if (from == ReservationStatus.NoShow && reservation.ReservedDate != clock.TodayLocal)
        {
            return Result.Failure(ErrorCodes.InvalidTransition);
        }

        // BR-RSV-23: lễ tân phải gọi xác minh trước khi xác nhận
        return to == ReservationStatus.Confirmed && reservation.ContactConfirmedAtUtc is null
            ? Result.Failure(ErrorCodes.ContactNotConfirmed)
            : null;
    }

    private void Notify(Reservation reservation, ReservationStatus from, ReservationStatus to, ReservationActor actor, string? reason)
    {
        switch (to)
        {
            case ReservationStatus.Confirmed:
                emails.EnqueueConfirmed(reservation, accessToken: null);
                break;
            case ReservationStatus.Cancelled when actor == ReservationActor.System:
                emails.EnqueueAutoCancelled(reservation);
                break;
            case ReservationStatus.Cancelled when actor == ReservationActor.Staff && from == ReservationStatus.Pending:
                emails.EnqueueRejected(reservation, reason?.Trim() ?? string.Empty);
                break;
            case ReservationStatus.Cancelled:
                emails.EnqueueCancelled(reservation);
                break;
        }
    }
}
