using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Vouchers.Abstractions;

namespace ObentoTensho.Core.Vouchers.Domain;

// docs/srs/14 §4: điều kiện hợp lệ (BR-VCH-02/06/08), mức giảm (BR-VCH-03), voucher cần duyệt (BR-VCH-21)
public static class VoucherRules
{
    public sealed record Context(DateTime NowUtc, decimal SubTotal, Guid? UserId, MemberTier? Tier, int UsedByUser);

    // Không tồn tại / tắt / chưa duyệt / ngoài hạn / không phải chủ ⇒ cùng một mã lỗi (BR-VCH-12)
    public static string? Check(Voucher? voucher, Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (voucher is null || !voucher.IsActive || voucher.ApprovalStatus != ApprovalStatus.Approved
            || (voucher.StartUtc is { } start && context.NowUtc < start)
            || (voucher.EndUtc is { } end && context.NowUtc >= end)
            || (voucher.OwnerUserId is { } owner && owner != context.UserId))
        {
            return ErrorCodes.VoucherInvalid;
        }

        if (voucher.RequireLogin && context.UserId is null)
        {
            return ErrorCodes.VoucherLoginRequired;
        }

        if (context.SubTotal < voucher.MinOrderValue)
        {
            return ErrorCodes.VoucherMinOrder;
        }

        if (voucher.ApplicableTier is { } tier && tier != context.Tier)
        {
            return ErrorCodes.VoucherTierMismatch;
        }

        var exhausted = (voucher.MaxUsageTotal is { } max && voucher.UsedCount >= max)
            || (context.UserId is not null && context.UsedByUser >= voucher.MaxUsagePerUser);
        return exhausted ? ErrorCodes.VoucherExhausted : null;
    }

    // %: ⌊tạm tính × % / 100⌋ rồi chặn trần; cố định: giá trị; không bao giờ vượt tạm tính
    public static decimal Discount(Voucher voucher, decimal subTotal)
    {
        ArgumentNullException.ThrowIfNull(voucher);
        var discount = voucher.DiscountType == DiscountType.Percent
            ? decimal.Floor(subTotal * voucher.DiscountValue / 100m)
            : voucher.DiscountValue;
        if (voucher.DiscountType == DiscountType.Percent && voucher.MaxDiscountAmount is { } cap)
        {
            discount = Math.Min(discount, cap);
        }

        return Math.Clamp(discount, 0, subTotal);
    }

    // BR-VCH-21: giảm từ ngưỡng % hoặc số tiền, hoặc không giới hạn lượt
    public static bool NeedsApproval(DiscountType type, decimal value, int? maxUsageTotal, IVoucherSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return maxUsageTotal is null
            || (type == DiscountType.Percent && value >= settings.ApprovalPercent)
            || (type == DiscountType.Fixed && value >= settings.ApprovalAmount);
    }
}
