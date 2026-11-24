using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Loyalty;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Loyalty.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Loyalty.Services;

// docs/srs/13 §7: khách xem điểm/lịch sử; quản lý xem, điều chỉnh điểm có lý do, duyệt bốn mắt khi vượt ngưỡng (BR-LOY-21)
public sealed class LoyaltyService(
    ILoyaltyRepository loyalty,
    LoyaltyLedger ledger,
    ILoyaltySettings settings,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<AdjustPointsRequest> validator)
{
    public const int MaxPageSize = 100;
    private const int AdjustmentListLimit = 200;
    private const string Entity = "loyalty_adjustment";

    public async Task<Result<LoyaltySummaryDto>> SummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await loyalty.FindUserAsync(userId, cancellationToken) is not { IsDeleted: false } user)
        {
            return Result.Failure<LoyaltySummaryDto>(ErrorCodes.NotFound);
        }

        var (points, next) = await loyalty.ExpiringAsync(userId, clock.UtcNow.AddDays(settings.ExpiryNoticeDays), cancellationToken);
        return Result.Success(user.ToSummary(settings, points, next));
    }

    public async Task<PagedResponse<LoyaltyEntryDto>> HistoryAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (items, total) = await loyalty.ListEntriesAsync(userId, page, pageSize, cancellationToken);
        return new PagedResponse<LoyaltyEntryDto>(items.Select(e => e.ToDto()).ToArray(), new PageMeta(page, pageSize, total));
    }

    // Tỷ lệ giảm theo hạng cho đơn của thành viên (BR-ORD-03); khách vãng lai = 0
    public async Task<decimal> MemberDiscountRateAsync(Guid? userId, CancellationToken cancellationToken) =>
        userId is { } id && await loyalty.FindUserAsync(id, cancellationToken) is { IsDeleted: false } user
            ? LoyaltyRules.DiscountPercent(user.MemberTier, settings) / 100m
            : 0m;

    // BR-LOY-11/21: dưới ngưỡng ghi ngay; vượt ngưỡng thì tạo đề nghị chờ người khác duyệt
    public async Task<Result<LoyaltyAdjustmentDto>> AdjustAsync(Guid userId, AdjustPointsRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<LoyaltyAdjustmentDto>();
        }

        if (await loyalty.FindUserAsync(userId, cancellationToken) is not { IsDeleted: false })
        {
            return Result.Failure<LoyaltyAdjustmentDto>(ErrorCodes.NotFound);
        }

        var adjustment = new LoyaltyAdjustment
        {
            UserId = userId,
            Points = request.Points,
            Reason = request.Reason.Trim(),
            RequestedByStaffId = staffId,
            RequestedAtUtc = clock.UtcNow,
        };

        return await unitOfWork.TryInTransactionAsync(async ct =>
        {
            loyalty.AddAdjustment(adjustment);
            if (Math.Abs(request.Points) > settings.AdjustApprovalThreshold)
            {
                audit.Record(new AuditEntry("loyalty.adjustment.requested", Entity, Id(adjustment), After: new { adjustment.UserId, adjustment.Points, adjustment.Reason }));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Success(adjustment.ToDto());
            }

            return await ApplyAsync(adjustment, staffId, note: null, ct);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<LoyaltyAdjustmentDto>> ListAdjustmentsAsync(ApprovalStatus? status, CancellationToken cancellationToken)
    {
        var adjustments = await loyalty.ListAdjustmentsAsync(status, AdjustmentListLimit, cancellationToken);
        var names = await loyalty.UserNamesAsync(adjustments.Select(a => a.UserId).Distinct().ToArray(), cancellationToken);
        return adjustments.Select(a => a.ToDto(names.GetValueOrDefault(a.UserId))).ToArray();
    }

    // Người duyệt phải khác người đề nghị (nguyên tắc bốn mắt)
    public Task<Result<LoyaltyAdjustmentDto>> ApproveAsync(Guid id, Guid staffId, string? note, CancellationToken cancellationToken) =>
        ReviewAsync(id, staffId, (adjustment, ct) => ApplyAsync(adjustment, staffId, note, ct), cancellationToken);

    public Task<Result<LoyaltyAdjustmentDto>> RejectAsync(Guid id, Guid staffId, string? note, CancellationToken cancellationToken) =>
        ReviewAsync(id, staffId, async (adjustment, ct) =>
        {
            adjustment.Status = ApprovalStatus.Rejected;
            adjustment.ReviewedByStaffId = staffId;
            adjustment.ReviewedAtUtc = clock.UtcNow;
            adjustment.ReviewNote = Clean(note);
            audit.Record(new AuditEntry("loyalty.adjustment.rejected", Entity, Id(adjustment), After: new { adjustment.ReviewNote }));
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(adjustment.ToDto());
        }, cancellationToken);

    private Task<Result<LoyaltyAdjustmentDto>> ReviewAsync(
        Guid id,
        Guid staffId,
        Func<LoyaltyAdjustment, CancellationToken, Task<Result<LoyaltyAdjustmentDto>>> review,
        CancellationToken cancellationToken) =>
        unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (await loyalty.FindAdjustmentAsync(id, ct) is not { } adjustment)
            {
                return Result.Failure<LoyaltyAdjustmentDto>(ErrorCodes.NotFound);
            }

            if (adjustment.Status != ApprovalStatus.Pending)
            {
                return Result.Failure<LoyaltyAdjustmentDto>(ErrorCodes.InvalidTransition);
            }

            return adjustment.RequestedByStaffId == staffId
                ? Result.Failure<LoyaltyAdjustmentDto>(ErrorCodes.FourEyesRequired)
                : await review(adjustment, ct);
        }, cancellationToken);

    private async Task<Result<LoyaltyAdjustmentDto>> ApplyAsync(LoyaltyAdjustment adjustment, Guid reviewerId, string? note, CancellationToken cancellationToken)
    {
        var before = await loyalty.FindUserAsync(adjustment.UserId, cancellationToken);
        var beforePoints = before?.CurrentPoints;
        var applied = await ledger.AdjustAsync(adjustment, cancellationToken);
        if (!applied.IsSuccess)
        {
            return Result.Failure<LoyaltyAdjustmentDto>(applied.ErrorCode ?? ErrorCodes.PointsInsufficient);
        }

        adjustment.Status = ApprovalStatus.Approved;
        adjustment.ReviewedByStaffId = reviewerId;
        adjustment.ReviewedAtUtc = clock.UtcNow;
        adjustment.ReviewNote = Clean(note);
        var after = await loyalty.FindUserAsync(adjustment.UserId, cancellationToken);
        audit.Record(new AuditEntry(
            "loyalty.adjustment.applied",
            Entity,
            Id(adjustment),
            new { CurrentPoints = beforePoints },
            new { adjustment.UserId, adjustment.Points, adjustment.Reason, after?.CurrentPoints }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(adjustment.ToDto());
    }

    private static string Id(LoyaltyAdjustment adjustment) => adjustment.Id.ToString("D", CultureInfo.InvariantCulture);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
