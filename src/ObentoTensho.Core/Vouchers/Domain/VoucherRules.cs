using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Vouchers.Abstractions;

namespace ObentoTensho.Core.Vouchers.Domain;

// docs/srs/14 §4: điều kiện hợp lệ (BR-VCH-02/06/08), mức giảm (BR-VCH-03), voucher cần duyệt (BR-VCH-21)
public static class VoucherRules
{
    // ShippingFee: phí ship của đơn (0 nếu không giao hàng); mã giảm ship chỉ dùng khi có phí ship
    public sealed record Context(DateTime NowUtc, decimal SubTotal, Guid? UserId, MemberTier? Tier, int UsedByUser, decimal ShippingFee = 0);

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

        if (voucher.AppliesTo == VoucherTarget.Shipping && context.ShippingFee <= 0)
        {
            return ErrorCodes.VoucherShippingOnly;
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
    // Gốc tính giảm là tạm tính, hoặc phí ship với mã giảm ship
    public static decimal Discount(Voucher voucher, decimal subTotal, decimal shippingFee = 0)
    {
        ArgumentNullException.ThrowIfNull(voucher);
        var basis = voucher.AppliesTo == VoucherTarget.Shipping ? shippingFee : subTotal;
        var discount = voucher.DiscountType == DiscountType.Percent
            ? decimal.Floor(basis * voucher.DiscountValue / 100m)
            : voucher.DiscountValue;
        if (voucher.DiscountType == DiscountType.Percent && voucher.MaxDiscountAmount is { } cap)
        {
            discount = Math.Min(discount, cap);
        }

        return Math.Clamp(discount, 0, basis);
    }

    // BR-VCH-21: giảm từ ngưỡng % hoặc số tiền, hoặc không giới hạn lượt. Mã giảm ship (tối đa bằng phí ship, vài chục nghìn)
    // chỉ cần duyệt khi không giới hạn lượt — free ship 100% là cách dùng thường ngày
    public static bool NeedsApproval(DiscountType type, decimal value, int? maxUsageTotal, IVoucherSettings settings, VoucherTarget target = VoucherTarget.Items)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return maxUsageTotal is null
            || (target == VoucherTarget.Items && type == DiscountType.Percent && value >= settings.ApprovalPercent)
            || (target == VoucherTarget.Items && type == DiscountType.Fixed && value >= settings.ApprovalAmount);
    }
}
