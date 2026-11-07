using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Realtime.Abstractions;
using ObentoTensho.Core.Realtime.Domain;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Reservations.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Reservations.Services;

// DB-20: một transaction — giữ chỗ có điều kiện, lấy số thứ tự ngày, tạo đặt bàn + lịch sử + thư; hết chỗ thì không ghi gì
public sealed class ReservationBooking(
    IReservationRepository reservations,
    IReservationEmailOutbox emails,
    ISecureTokenService secureTokens,
    IUnitOfWork unitOfWork,
    IRealtimeEvents realtime,
    IClock clock)
{
    public Task<Result<Reservation>> BookAsync(BookingDraft draft, CancellationToken cancellationToken) =>
        unitOfWork.TryInTransactionAsync(async ct =>
        {
            var guests = draft.AdultCount + draft.ChildCount;
            var force = draft.OverrideCapacityReason is not null;
            if (!await reservations.TryReserveCapacityAsync(draft.Slot.Id, draft.Date, guests, draft.Slot.MaxReservations, draft.Slot.MaxGuests, force, ct))
            {
                return Result.Failure<Reservation>(ErrorCodes.SlotFull);
            }

            var now = clock.UtcNow;
            var token = secureTokens.Create();
            var reservation = NewReservation(draft, await reservations.NextDailySeqAsync(draft.Date, ct), token.Hash, now);
            reservations.Add(reservation);
            AddCreatedHistory(reservation, draft, now);
            Notify(reservation, token.Value);
            realtime.Stage(RealtimeMessages.ReservationCreated(reservation));
            realtime.Stage(RealtimeMessages.Availability(reservation));

            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(reservation);
        }, cancellationToken);

    private Reservation NewReservation(BookingDraft draft, int dailySeq, string tokenHash, DateTime now)
    {
        // Lễ tân tạo hộ qua điện thoại/tại quầy coi như đã xác minh liên hệ (BR-RSV-23)
        var byStaff = draft.StaffId is not null;
        return new Reservation
        {
            ReservationCode = ReservationCodes.New(),
            DailySeq = dailySeq,
            AccessTokenHash = tokenHash,
            UserId = draft.UserId,
            GuestName = draft.GuestName,
            GuestPhone = draft.GuestPhone,
            GuestEmail = draft.GuestEmail,
            Source = draft.Source,
            CreatedByStaffId = draft.StaffId,
            ContactConfirmedAtUtc = byStaff ? now : null,
            ConfirmedByStaffId = draft.StaffId,
            OtpRequired = draft.ContactVerified,
            ContactVerifiedAtUtc = draft.ContactVerified ? now : null,
            Lang = draft.Lang,
            ReservedDate = draft.Date,
            ReservedTime = draft.Time,
            ReservedAtUtc = clock.ToUtc(draft.Date, draft.Time),
            SlotId = draft.Slot.Id,
            AdultCount = draft.AdultCount,
            ChildCount = draft.ChildCount,
            SpecialRequest = draft.SpecialRequest,
            InternalNote = draft.InternalNote,
            Status = draft.InitialStatus,
            CreatedAtUtc = now,
        };
    }

    private void AddCreatedHistory(Reservation reservation, BookingDraft draft, DateTime now)
    {
        var actor = draft.StaffId is null ? ReservationActor.Guest : ReservationActor.Staff;
        var created = draft.InitialStatus == ReservationStatus.CheckedIn ? ReservationStatus.Confirmed : draft.InitialStatus;
        reservations.AddHistory(new ReservationStatusHistory
        {
            ReservationId = reservation.Id,
            ToStatus = created,
            ChangedByType = actor,
            ChangedById = draft.StaffId,
            Reason = draft.OverrideCapacityReason is { } reason ? $"Vượt sức chứa: {reason}" : null,
            ChangedAtUtc = now,
        });

        if (draft.InitialStatus == ReservationStatus.CheckedIn)
        {
            reservations.AddHistory(new ReservationStatusHistory
            {
                ReservationId = reservation.Id,
                FromStatus = ReservationStatus.Confirmed,
                ToStatus = ReservationStatus.CheckedIn,
                ChangedByType = actor,
                ChangedById = draft.StaffId,
                ChangedAtUtc = now,
            });
        }
    }

    private void Notify(Reservation reservation, string accessToken)
    {
        switch (reservation.Status)
        {
            case ReservationStatus.Pending:
                emails.EnqueueReceived(reservation, accessToken);
                break;
            case ReservationStatus.Confirmed:
                emails.EnqueueConfirmed(reservation, accessToken);
                break;
        }
    }
}
