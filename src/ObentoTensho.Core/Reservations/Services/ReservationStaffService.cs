using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos.Reservations;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Reservations.Services;

// FR-RSV-06 (lễ tân tạo hộ khách) và gán bàn (BR-RSV-12)
public sealed class ReservationStaffService(
    IReservationRepository reservations,
    ITableLayoutRepository tables,
    ReservationSchedule schedule,
    ReservationBooking booking,
    ReservationTransitions transitions,
    IReservationSettings settings,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IValidator<StaffCreateReservationRequest> validator)
{
    private const string Entity = "reservation";

    // Không áp giới hạn theo SĐT và OTP (khách đã nói chuyện trực tiếp với lễ tân); vượt sức chứa chỉ Manager+, có lý do
    public async Task<Result<AdminReservationDto>> CreateAsync(StaffCreateReservationRequest request, Guid staffId, StaffRole role, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<AdminReservationDto>();
        }

        var overrideReason = string.IsNullOrWhiteSpace(request.OverrideCapacityReason) ? null : request.OverrideCapacityReason.Trim();
        if (overrideReason is not null && role is not (StaffRole.SuperAdmin or StaffRole.Manager))
        {
            return Result.Failure<AdminReservationDto>(ErrorCodes.Forbidden);
        }

        var slot = await schedule.CheckBookableAsync(request.Date, request.Time, staff: true, cancellationToken);
        if (!slot.IsSuccess || !VietnamesePhoneNumber.TryNormalize(request.GuestPhone, out var phone))
        {
            return Result.Failure<AdminReservationDto>(slot.ErrorCode ?? ErrorCodes.ValidationFailed);
        }

        var draft = new BookingDraft(
            request.Date,
            request.Time,
            slot.Value,
            request.AdultCount,
            request.ChildCount,
            request.GuestName.Trim(),
            phone,
            string.IsNullOrWhiteSpace(request.GuestEmail) ? null : EmailAddress.Normalize(request.GuestEmail),
            Clean(request.SpecialRequest),
            Clean(request.InternalNote),
            AuthPolicy.DefaultLanguage,
            request.Source,
            UserId: null,
            staffId,
            request.CheckInNow ? ReservationStatus.CheckedIn : ReservationStatus.Confirmed,
            overrideReason);

        var created = await booking.BookAsync(draft, cancellationToken);
        if (!created.IsSuccess)
        {
            return Result.Failure<AdminReservationDto>(created.ErrorCode ?? ErrorCodes.SlotFull);
        }

        if (overrideReason is not null)
        {
            audit.Record(new AuditEntry("reservations.reservation.capacity_overridden", Entity, Id(created.Value.Id), After: new { overrideReason }));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(created.Value.ToAdmin(noShowFlag: false, settings.LargePartySize));
    }

    // Bàn đang bật, vừa số khách, chưa có đặt bàn khác cùng ngày cùng ca (BR-TBL-17)
    public Task<Result<AdminReservationDto>> AssignTableAsync(Guid id, int? tableId, CancellationToken cancellationToken) =>
        unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (await reservations.FindForUpdateAsync(id, ct) is not { } reservation)
            {
                return Result.Failure<AdminReservationDto>(ErrorCodes.NotFound);
            }

            if (reservation.Status is not (ReservationStatus.Pending or ReservationStatus.Confirmed or ReservationStatus.CheckedIn))
            {
                return Result.Failure<AdminReservationDto>(ErrorCodes.InvalidTransition);
            }

            if (tableId is { } newTable && await CheckTableAsync(newTable, reservation, ct) is { } failure)
            {
                return Result.Failure<AdminReservationDto>(failure);
            }

            var before = reservation.TableId;
            reservation.TableId = null;
            await ReleaseTableAsync(before, ct);
            reservation.TableId = tableId;
            await transitions.SyncTableAsync(reservation, ct);
            audit.Record(new AuditEntry("reservations.reservation.table_assigned", Entity, Id(reservation.Id), new { tableId = before }, new { tableId }));
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(reservation.ToAdmin(noShowFlag: false, settings.LargePartySize));
        }, cancellationToken);

    private async Task<string?> CheckTableAsync(int tableId, Domain.Reservation reservation, CancellationToken cancellationToken)
    {
        var table = await tables.FindTableAsync(tableId, cancellationToken);
        if (table is null || !table.IsActive || reservation.GuestCount < table.MinCapacity || reservation.GuestCount > table.MaxCapacity)
        {
            return ErrorCodes.TableUnsuitable;
        }

        return await reservations.TableTakenAsync(tableId, reservation.ReservedDate, reservation.SlotId, reservation.Id, cancellationToken)
            ? ErrorCodes.TableConflict
            : null;
    }

    // Bỏ gán khỏi bàn cũ: trả bàn về Trống nếu đang giữ cho đặt bàn này
    private async Task ReleaseTableAsync(int? tableId, CancellationToken cancellationToken)
    {
        if (tableId is { } id && await tables.FindTableAsync(id, cancellationToken) is { CurrentStatus: TableStatus.Reserved or TableStatus.Occupied } table)
        {
            table.CurrentStatus = TableStatus.Available;
        }
    }

    private static string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
