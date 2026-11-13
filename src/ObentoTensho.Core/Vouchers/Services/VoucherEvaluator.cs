using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Vouchers.Abstractions;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Core.Vouchers.Services;

// Kiểm mã cho một tạm tính (BR-VCH-02), giữ lượt nguyên tử trong transaction tạo đơn (BR-VCH-07), hoàn lượt khi hủy (BR-ORD-07)
public sealed class VoucherEvaluator(IVoucherRepository vouchers, ILoyaltyRepository loyalty, IClock clock)
{
    public sealed record Evaluation(Voucher Voucher, decimal Discount);

    public async Task<Result<Evaluation>> EvaluateAsync(string? code, decimal subTotal, Guid? userId, CancellationToken cancellationToken, decimal shippingFee = 0)
    {
        var normalized = VoucherCodes.Normalize(code);
        var voucher = normalized.Length == 0 ? null : await vouchers.FindByCodeAsync(normalized, cancellationToken);
        var user = userId is { } id ? await loyalty.FindUserAsync(id, cancellationToken) : null;
        var used = voucher is not null && userId is { } uid ? await vouchers.UsedByUserAsync(voucher.Id, uid, cancellationToken) : 0;
        var error = VoucherRules.Check(voucher, new VoucherRules.Context(clock.UtcNow, subTotal, userId, user?.MemberTier, used, shippingFee));
        return error is null
            ? Result.Success(new Evaluation(voucher!, VoucherRules.Discount(voucher!, subTotal, shippingFee)))
            : Result.Failure<Evaluation>(error);
    }

    // false = vừa hết lượt (hoặc khách đã dùng đủ) giữa lúc kiểm và lúc ghi — bên gọi hoàn tác cả đơn
    public async Task<bool> ReserveAsync(Voucher voucher, Guid? userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(voucher);
        if (!await vouchers.TryUseAsync(voucher.Id, cancellationToken))
        {
            return false;
        }

        return userId is not { } id || await vouchers.TryUseForUserAsync(voucher.Id, id, voucher.MaxUsagePerUser, clock.UtcNow, cancellationToken);
    }

    public Task ReleaseAsync(int voucherId, Guid? userId, CancellationToken cancellationToken) =>
        vouchers.ReleaseAsync(voucherId, userId, cancellationToken);

    public static string ErrorOrInvalid(Result result) => result?.ErrorCode ?? ErrorCodes.VoucherInvalid;
}
