using ObentoTensho.Contracts.Dtos.Vouchers;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Core.Vouchers.Services;

public static class VoucherMappings
{
    public static AdminVoucherDto ToAdmin(this Voucher v, IClock clock) =>
        new(
            v.Id,
            v.Code,
            v.DiscountType,
            v.DiscountValue,
            v.MaxDiscountAmount,
            v.MinOrderValue,
            v.MaxUsageTotal,
            v.MaxUsagePerUser,
            v.UsedCount,
            v.ApplicableTier,
            v.RequireLogin,
            StartDate(v, clock),
            EndDate(v, clock),
            v.IsActive,
            v.Source,
            v.OwnerUserId,
            v.BatchId,
            v.PromotionId,
            v.ApprovalStatus,
            v.CreatedAtUtc,
            RowVersions.Encode(v.RowVersion));

    public static AccountVoucherDto ToAccount(this Voucher v, IClock clock, int usedByUser)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var usable = v.IsActive && v.ApprovalStatus == ApprovalStatus.Approved
            && (v.EndUtc is not { } end || clock.UtcNow < end)
            && (v.MaxUsageTotal is not { } max || v.UsedCount < max)
            && usedByUser < v.MaxUsagePerUser;
        return new AccountVoucherDto(v.Code, v.DiscountType, v.DiscountValue, v.MaxDiscountAmount, v.MinOrderValue, EndDate(v, clock), v.Source, usable, v.CreatedAtUtc);
    }

    // Ngày VN: bắt đầu = ngày của StartUtc; kết thúc = ngày trước EndUtc (EndUtc là đầu ngày hôm sau)
    public static DateOnly? StartDate(Voucher v, IClock clock) =>
        v.StartUtc is { } start ? DateOnly.FromDateTime(clock.ToLocal(start)) : null;

    public static DateOnly? EndDate(Voucher v, IClock clock) =>
        v.EndUtc is { } end ? DateOnly.FromDateTime(clock.ToLocal(end).AddTicks(-1)) : null;
}
