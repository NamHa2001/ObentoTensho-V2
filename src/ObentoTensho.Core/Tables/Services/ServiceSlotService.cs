using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Tables.Domain;

namespace ObentoTensho.Core.Tables.Services;

// Ca phục vụ (BR-TBL-03, BR-TBL-15): ca đang bật không chồng lấn và nằm trọn trong một khoảng mở cửa
public sealed class ServiceSlotService(
    IScheduleRepository schedule,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    IReservationRepository reservations,
    IPublicScheduleCache publicCache,
    IValidator<ServiceSlotRequest> validator)
{
    private const string Entity = "service_slot";

    public async Task<IReadOnlyList<ServiceSlotDto>> ListAsync(CancellationToken cancellationToken) =>
        (await schedule.ListSlotsAsync(cancellationToken))
            .OrderBy(s => s.DayOfWeek ?? -1).ThenBy(s => s.StartTime)
            .Select(s => s.ToDto())
            .ToArray();

    public async Task<Result<ServiceSlotDto>> CreateAsync(ServiceSlotRequest request, CancellationToken cancellationToken)
    {
        if (await CheckAsync(request, exceptId: null, cancellationToken) is { } failure)
        {
            return Result.Failure<ServiceSlotDto>(failure.ErrorCode ?? ErrorCodes.ValidationFailed, failure.FieldErrors);
        }

        return await unitOfWork.TryCreateAsync(async ct =>
        {
            var slot = ToSlot(request);
            schedule.AddSlot(slot);
            await unitOfWork.SaveChangesAsync(ct);
            audit.Record(new AuditEntry(AuditActions.For(Entity, AuditActions.Created), Entity, slot.Id.ToString(CultureInfo.InvariantCulture), After: request));
            await unitOfWork.SaveChangesAsync(ct);
            publicCache.Invalidate();
            return slot.ToDto();
        }, ErrorCodes.SlotOverlap, cancellationToken);
    }

    public async Task<Result<ServiceSlotDto>> UpdateAsync(int id, ServiceSlotRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        if (await CheckAsync(request, id, cancellationToken) is { } failure)
        {
            return Result.Failure<ServiceSlotDto>(failure.ErrorCode ?? ErrorCodes.ValidationFailed, failure.FieldErrors);
        }

        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure<ServiceSlotDto>(ErrorCodes.PreconditionRequired);
        }

        if (await schedule.FindSlotAsync(id, cancellationToken) is not { } slot)
        {
            return Result.Failure<ServiceSlotDto>(ErrorCodes.NotFound);
        }

        var before = slot.ToDto() with { Version = string.Empty };
        unitOfWork.ExpectRowVersion(slot, version);
        slot.DayOfWeek = (byte?)request.DayOfWeek;
        slot.StartTime = request.Start;
        slot.EndTime = request.End;
        slot.MaxReservations = request.MaxReservations;
        slot.MaxGuests = request.MaxGuests;
        slot.IsActive = request.IsActive;
        audit.Record(new AuditEntry(AuditActions.For(Entity, AuditActions.Updated), Entity, id.ToString(CultureInfo.InvariantCulture), before, request));

        var result = await unitOfWork.TrySaveAsync(slot.ToDto, ErrorCodes.SlotOverlap, cancellationToken);
        publicCache.Invalidate();
        return result;
    }

    // BR-TBL-06: ca đã có đặt bàn thì chỉ được tắt, không xóa (lịch sử đặt bàn tham chiếu ca)
    public async Task<Result> DeleteAsync(int id, string? ifMatch, CancellationToken cancellationToken)
    {
        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure(ErrorCodes.PreconditionRequired);
        }

        if (await schedule.FindSlotAsync(id, cancellationToken) is not { } slot)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        if (await reservations.HasAnyForSlotAsync(id, cancellationToken))
        {
            return Result.Failure(ErrorCodes.SlotHasReservations);
        }

        unitOfWork.ExpectRowVersion(slot, version);
        schedule.RemoveSlot(slot);
        audit.Record(new AuditEntry(AuditActions.For(Entity, AuditActions.Deleted), Entity, id.ToString(CultureInfo.InvariantCulture), Before: slot.ToDto() with { Version = string.Empty }));
        var result = await unitOfWork.TrySaveAsync(ErrorCodes.SlotOverlap, cancellationToken);
        publicCache.Invalidate();
        return result;
    }

    private async Task<Result?> CheckAsync(ServiceSlotRequest request, int? exceptId, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        var candidate = ToSlot(request);
        if (!candidate.IsActive)
        {
            return null;
        }

        var others = (await schedule.ListSlotsAsync(cancellationToken)).Where(s => s.IsActive && s.Id != exceptId);
        if (others.Any(s => ScheduleRules.SlotsOverlap(s, candidate)))
        {
            return Result.Failure(ErrorCodes.SlotOverlap);
        }

        var weekly = await schedule.ListWeeklyAsync(forUpdate: false, cancellationToken);
        return ScheduleRules.SlotFitsOpeningHours(candidate, weekly) ? null : Result.Failure(ErrorCodes.SlotOutsideOpeningHours);
    }

    private static ServiceSlot ToSlot(ServiceSlotRequest request) => new()
    {
        DayOfWeek = (byte?)request.DayOfWeek,
        StartTime = request.Start,
        EndTime = request.End,
        MaxReservations = request.MaxReservations,
        MaxGuests = request.MaxGuests,
        IsActive = request.IsActive,
    };
}
