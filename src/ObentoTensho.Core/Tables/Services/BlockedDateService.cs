using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Tables.Abstractions;
using ObentoTensho.Core.Tables.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Tables.Services;

// Khóa ngày/khung giờ (BR-TBL-04, BR-TBL-18); có hiệu lực ngay với lịch nhận khách
public sealed class BlockedDateService(
    IScheduleRepository schedule,
    IUnitOfWork unitOfWork,
    IAuditTrail audit,
    IAuditContext actor,
    IPublicScheduleCache publicCache,
    IClock clock,
    IValidator<BlockedDateRequest> validator)
{
    private const string Entity = "blocked_date";

    public async Task<IReadOnlyList<BlockedDateDto>> ListAsync(CancellationToken cancellationToken) =>
        (await schedule.ListBlockedAsync(clock.TodayLocal, cancellationToken)).Select(b => b.ToDto()).ToArray();

    public async Task<Result<BlockedDateDto>> CreateAsync(BlockedDateRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<BlockedDateDto>();
        }

        if (actor.Actor.Id is not { } staffId)
        {
            return Result.Failure<BlockedDateDto>(ErrorCodes.Forbidden);
        }

        return await unitOfWork.TryCreateAsync(async ct =>
        {
            var blocked = new BlockedDate
            {
                Date = request.Date,
                AppliesTo = request.AppliesTo,
                StartTime = request.Start,
                EndTime = request.End,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                CreatedByStaffId = staffId,
                CreatedAtUtc = clock.UtcNow,
            };
            schedule.AddBlocked(blocked);
            await unitOfWork.SaveChangesAsync(ct);
            audit.Record(new AuditEntry(AuditActions.For(Entity, AuditActions.Created), Entity, blocked.Id.ToString(CultureInfo.InvariantCulture), After: request));
            await unitOfWork.SaveChangesAsync(ct);
            publicCache.Invalidate();
            return blocked.ToDto();
        }, ErrorCodes.DuplicateName, cancellationToken);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        if (await schedule.FindBlockedAsync(id, cancellationToken) is not { } blocked)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        schedule.RemoveBlocked(blocked);
        audit.Record(new AuditEntry(AuditActions.For(Entity, AuditActions.Deleted), Entity, id.ToString(CultureInfo.InvariantCulture), Before: blocked.ToDto()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        publicCache.Invalidate();
        return Result.Success();
    }
}
