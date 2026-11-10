using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos.Vouchers;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Vouchers.Abstractions;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Core.Vouchers.Services;

// Quán tự đặt danh mục phần thưởng đổi điểm; voucher đã đổi giữ nguyên điều kiện lúc đổi nên sửa hay xóa phần thưởng không ảnh hưởng khách
public sealed class RedeemRewardAdminService(
    IVoucherRepository vouchers,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<RedeemRewardRequest> validator)
{
    private const string Entity = "redeem_reward";

    public async Task<IReadOnlyList<AdminRedeemRewardDto>> ListAsync(CancellationToken cancellationToken) =>
        (await vouchers.ListRewardsAsync(activeOnly: false, cancellationToken)).Select(ToDto).ToArray();

    public async Task<Result<AdminRedeemRewardDto>> CreateAsync(RedeemRewardRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<AdminRedeemRewardDto>();
        }

        var order = (await vouchers.ListRewardsAsync(activeOnly: false, cancellationToken)).Count + 1;
        var reward = new RedeemReward { Name = request.Name.Trim(), DisplayOrder = order, CreatedAtUtc = clock.UtcNow };
        Apply(reward, request);
        return await unitOfWork.TryCreateAsync(async ct =>
        {
            vouchers.AddReward(reward);
            await unitOfWork.SaveChangesAsync(ct);
            audit.Record(new AuditEntry($"vouchers.{Entity}.created", Entity, Id(reward), After: Snapshot(reward)));
            await unitOfWork.SaveChangesAsync(ct);
            return ToDto(reward);
        }, ErrorCodes.DuplicateName, cancellationToken);
    }

    public async Task<Result<AdminRedeemRewardDto>> UpdateAsync(int id, RedeemRewardRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<AdminRedeemRewardDto>();
        }

        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure<AdminRedeemRewardDto>(ErrorCodes.PreconditionRequired);
        }

        if (await vouchers.FindRewardAsync(id, cancellationToken) is not { } reward)
        {
            return Result.Failure<AdminRedeemRewardDto>(ErrorCodes.NotFound);
        }

        unitOfWork.ExpectRowVersion(reward, version);
        var before = Snapshot(reward);
        Apply(reward, request);
        reward.UpdatedAtUtc = clock.UtcNow;
        audit.Record(new AuditEntry($"vouchers.{Entity}.updated", Entity, Id(reward), before, Snapshot(reward)));
        var saved = await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, cancellationToken);
        return saved.IsSuccess
            ? Result.Success(ToDto(reward))
            : Result.Failure<AdminRedeemRewardDto>(saved.ErrorCode ?? ErrorCodes.PreconditionFailed);
    }

    public async Task<Result> DeleteAsync(int id, string? ifMatch, CancellationToken cancellationToken)
    {
        if (RowVersions.Decode(ifMatch) is not { } version)
        {
            return Result.Failure(ErrorCodes.PreconditionRequired);
        }

        if (await vouchers.FindRewardAsync(id, cancellationToken) is not { } reward)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        unitOfWork.ExpectRowVersion(reward, version);
        audit.Record(new AuditEntry($"vouchers.{Entity}.deleted", Entity, Id(reward), Before: Snapshot(reward)));
        vouchers.RemoveReward(reward);
        return await unitOfWork.TrySaveAsync(ErrorCodes.DuplicateName, cancellationToken);
    }

    private static void Apply(RedeemReward reward, RedeemRewardRequest request)
    {
        reward.Name = request.Name.Trim();
        reward.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        reward.PointsCost = request.PointsCost;
        reward.DiscountType = request.DiscountType;
        reward.DiscountValue = request.DiscountValue;
        reward.MaxDiscountAmount = request.MaxDiscountAmount;
        reward.MinOrderValue = request.MinOrderValue;
        reward.ValidDays = request.ValidDays;
        reward.IsActive = request.IsActive;
    }

    private static object Snapshot(RedeemReward r) => new { r.Name, r.PointsCost, r.DiscountType, r.DiscountValue, r.MaxDiscountAmount, r.MinOrderValue, r.ValidDays, r.IsActive };

    private static AdminRedeemRewardDto ToDto(RedeemReward r) =>
        new(r.Id, r.Name, r.Description, r.PointsCost, r.DiscountType, r.DiscountValue, r.MaxDiscountAmount, r.MinOrderValue, r.ValidDays, r.IsActive, r.DisplayOrder, RowVersions.Encode(r.RowVersion));

    private static string Id(RedeemReward reward) => reward.Id.ToString(CultureInfo.InvariantCulture);
}
