using System.Globalization;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Reservations;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Realtime.Abstractions;
using ObentoTensho.Core.Realtime.Domain;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Reservations.Domain;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Reservations.Services;

// FR-RSV-02, FR-RSV-05: màn hình lễ tân — danh sách, chi tiết, gọi xác minh, đổi trạng thái, ghi chú
public sealed class ReservationDeskService(
    IReservationRepository reservations,
    IScheduleRepository schedule,
    ReservationTransitions transitions,
    IReservationSettings settings,
    IActionThrottle throttle,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IRealtimeEvents realtime,
    IClock clock)
{
    public const int MaxPageSize = 100;

    // BR-CUS-09: mỗi nhân viên "Hiện liên hệ" tối đa 100 lần/giờ
    private const int RevealLimitPerHour = 100;
    private const string Entity = "reservation";
    private static readonly TimeSpan NoShowWindow = TimeSpan.FromDays(90);

    public async Task<PagedResponse<AdminReservationDto>> ListAsync(ReservationQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var (items, total) = await reservations.ListAsync(query with { Page = page, PageSize = pageSize }, cancellationToken);
        var flagged = await FlaggedAsync(items.Select(r => r.GuestPhone), cancellationToken);
        return new PagedResponse<AdminReservationDto>(
            items.Select(r => r.ToAdmin(flagged.Contains(r.GuestPhone), settings.LargePartySize)).ToArray(),
            new PageMeta(page, pageSize, total));
    }

    public async Task<Result<AdminReservationDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await reservations.FindForUpdateAsync(id, cancellationToken) is not { } reservation)
        {
            return Result.Failure<AdminReservationDetailDto>(ErrorCodes.NotFound);
        }

        var flagged = await FlaggedAsync([reservation.GuestPhone], cancellationToken);
        var history = await reservations.GetHistoryAsync(id, cancellationToken);
        return Result.Success(new AdminReservationDetailDto(
            reservation.ToAdmin(flagged.Count > 0, settings.LargePartySize),
            history.Select(h => h.ToDto()).ToArray()));
    }

    // BR-RSV-23, BR-CUS-09: xem liên hệ đầy đủ để gọi — mỗi lần đều ghi audit, có giới hạn theo nhân viên
    public async Task<Result<ReservationContactDto>> RevealContactAsync(Guid id, Guid staffId, CancellationToken cancellationToken)
    {
        if (throttle.Count($"reveal-contact:{staffId:N}") >= RevealLimitPerHour)
        {
            return Result.Failure<ReservationContactDto>(ErrorCodes.RateLimited);
        }

        if (await reservations.FindForUpdateAsync(id, cancellationToken) is not { } reservation)
        {
            return Result.Failure<ReservationContactDto>(ErrorCodes.NotFound);
        }

        throttle.Increment($"reveal-contact:{staffId:N}", TimeSpan.FromHours(1));
        audit.Record(new AuditEntry("reservations.reservation.contact_revealed", Entity, Id(reservation)));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new ReservationContactDto(reservation.GuestPhone, reservation.GuestEmail));
    }

    // Không nghe máy: ghi lần gọi (tối đa reservation.callAttempts)
    public Task<Result<AdminReservationDto>> RecordCallAttemptAsync(Guid id, string? note, CancellationToken cancellationToken) =>
        UpdateAsync(id, reservation =>
        {
            if (reservation.Status != ReservationStatus.Pending)
            {
                return Result.Failure(ErrorCodes.InvalidTransition);
            }

            if (reservation.CallAttempts >= settings.CallAttempts)
            {
                return Result.Failure(ErrorCodes.CallAttemptsExceeded);
            }

            reservation.CallAttempts++;
            audit.Record(new AuditEntry("reservations.reservation.call_attempt", Entity, Id(reservation), After: new { reservation.CallAttempts, note }));
            return Result.Success();
        }, cancellationToken);

    // BR-RSV-23: "Đã gọi xác minh" kèm ghi chú cuộc gọi — điều kiện để Xác nhận
    public Task<Result<AdminReservationDto>> ConfirmContactAsync(Guid id, Guid staffId, string? note, CancellationToken cancellationToken) =>
        UpdateAsync(id, reservation =>
        {
            if (reservation.Status is not (ReservationStatus.Pending or ReservationStatus.Confirmed))
            {
                return Result.Failure(ErrorCodes.InvalidTransition);
            }

            reservation.ContactConfirmedAtUtc = clock.UtcNow;
            reservation.ConfirmedByStaffId = staffId;
            reservation.ConfirmNote = Clean(note);
            return Result.Success();
        }, cancellationToken);

    public Task<Result<AdminReservationDto>> SetInternalNoteAsync(Guid id, string? note, CancellationToken cancellationToken) =>
        UpdateAsync(id, reservation =>
        {
            reservation.InternalNote = Clean(note);
            return Result.Success();
        }, cancellationToken);

    // DB-23: ExpectedStatus phải khớp — hai lễ tân bấm cùng lúc thì chỉ một người thắng
    public Task<Result<AdminReservationDto>> ChangeStatusAsync(Guid id, ChangeReservationStatusRequest request, Guid staffId, CancellationToken cancellationToken) =>
        unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (await reservations.FindForUpdateAsync(id, ct) is not { } reservation)
            {
                return Result.Failure<AdminReservationDto>(ErrorCodes.NotFound);
            }

            if (reservation.Status != request.ExpectedStatus)
            {
                return Result.Failure<AdminReservationDto>(ErrorCodes.InvalidTransition);
            }

            var applied = await transitions.ApplyAsync(reservation, request.Status, ReservationActor.Staff, staffId, Clean(request.Reason), ct);
            if (!applied.IsSuccess)
            {
                return Result.Failure<AdminReservationDto>(applied.ErrorCode ?? ErrorCodes.InvalidTransition, applied.FieldErrors);
            }

            return await SaveAsync(reservation, ct);
        }, cancellationToken);

    // Tối đa 31 ngày một lần để truy vấn nhỏ
    public async Task<Result<IReadOnlyList<CalendarDayDto>>> CalendarAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        if (toDate < fromDate || toDate.DayNumber - fromDate.DayNumber > 30)
        {
            return Result.Failure<IReadOnlyList<CalendarDayDto>>(ErrorCodes.ValidationFailed);
        }

        var slots = await schedule.ListSlotsAsync(cancellationToken);
        var capacities = (await reservations.GetCapacitiesAsync(fromDate, toDate, cancellationToken)).ToLookup(c => c.Date);
        var days = new List<CalendarDayDto>();
        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            var booked = capacities[date].ToDictionary(c => c.SlotId);
            var day = (byte)date.DayOfWeek;
            days.Add(new CalendarDayDto(date, slots
                .Where(s => s.IsActive && s.AppliesTo(day))
                .OrderBy(s => s.StartTime)
                .Select(s => new CalendarSlotDto(
                    s.Id, s.StartTime, s.EndTime,
                    booked.GetValueOrDefault(s.Id)?.BookedCount ?? 0, s.MaxReservations,
                    booked.GetValueOrDefault(s.Id)?.BookedGuests ?? 0, s.MaxGuests))
                .ToArray()));
        }

        return Result.Success<IReadOnlyList<CalendarDayDto>>(days);
    }

    private Task<Result<AdminReservationDto>> UpdateAsync(Guid id, Func<Reservation, Result> change, CancellationToken cancellationToken) =>
        unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (await reservations.FindForUpdateAsync(id, ct) is not { } reservation)
            {
                return Result.Failure<AdminReservationDto>(ErrorCodes.NotFound);
            }

            var changed = change(reservation);
            if (!changed.IsSuccess)
            {
                return Result.Failure<AdminReservationDto>(changed.ErrorCode ?? ErrorCodes.ValidationFailed, changed.FieldErrors);
            }

            reservation.UpdatedAtUtc = clock.UtcNow;
            return await SaveAsync(reservation, ct);
        }, cancellationToken);

    private async Task<Result<AdminReservationDto>> SaveAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
        realtime.Stage(RealtimeMessages.ReservationChanged(reservation));
        var flagged = await FlaggedAsync([reservation.GuestPhone], cancellationToken);
        return Result.Success(reservation.ToAdmin(flagged.Count > 0, settings.LargePartySize));
    }

    private Task<IReadOnlySet<string>> FlaggedAsync(IEnumerable<string> phones, CancellationToken cancellationToken) =>
        reservations.FlaggedPhonesAsync(phones.Distinct(StringComparer.Ordinal).ToArray(), clock.UtcNow - NoShowWindow, settings.NoShowFlagCount, cancellationToken);

    private static string Id(Reservation reservation) => reservation.Id.ToString("D", CultureInfo.InvariantCulture);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
