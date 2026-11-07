using FluentValidation;
using ObentoTensho.Contracts.Dtos.Vouchers;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Loyalty.Services;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Orders.Services;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Vouchers.Abstractions;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Core.Vouchers.Services;

// Phía khách: kiểm mã ở giỏ (BR-VCH-02/19), voucher của tôi, đổi điểm lấy voucher (BR-LOY-05/07)
public sealed class VoucherService(
    IVoucherRepository vouchers,
    VoucherEvaluator evaluator,
    OrderCart cart,
    LoyaltyService loyaltyService,
    ILoyaltyRepository loyalty,
    LoyaltyLedger ledger,
    ILoyaltySettings loyaltySettings,
    IVoucherSettings settings,
    IVoucherEmailOutbox emails,
    IActionThrottle throttle,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<ValidateVoucherRequest> validator)
{
    private const string RedeemPrefix = "DIEM";

    // BR-VCH-12: ngoài giới hạn theo IP, mỗi tài khoản tối đa 30 lần kiểm mã mỗi giờ
    private const int ValidatePerHour = 30;

    public async Task<Result<VoucherCheckDto>> ValidateAsync(ValidateVoucherRequest request, Guid? userId, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<VoucherCheckDto>();
        }

        if (userId is { } id)
        {
            var key = $"voucher-validate:{id:N}";
            if (throttle.Count(key) >= ValidatePerHour)
            {
                return Result.Failure<VoucherCheckDto>(ErrorCodes.RateLimited);
            }

            throttle.Increment(key, TimeSpan.FromHours(1));
        }

        var priced = await cart.PriceAsync(request.Items, AuthPolicy.DefaultLanguage, cancellationToken);
        if (!priced.IsValid)
        {
            return Result.Failure<VoucherCheckDto>(ErrorCodes.ItemUnavailable);
        }

        var evaluated = await evaluator.EvaluateAsync(request.Code, priced.SubTotal, userId, cancellationToken);
        if (!evaluated.IsSuccess)
        {
            return Result.Failure<VoucherCheckDto>(VoucherEvaluator.ErrorOrInvalid(evaluated));
        }

        var withVoucher = OrderPricing.Compute(priced.SubTotal, memberRate: 0, evaluated.Value.Discount);
        var withMember = OrderPricing.Compute(priced.SubTotal, await loyaltyService.MemberDiscountRateAsync(userId, cancellationToken), voucherDiscount: 0);
        return Result.Success(new VoucherCheckDto(
            evaluated.Value.Voucher.Code,
            priced.SubTotal,
            withVoucher.VoucherDiscount,
            withVoucher.TotalAmount,
            withMember.MemberDiscount,
            withMember.TotalAmount,
            withMember.TotalAmount < withVoucher.TotalAmount));
    }

    public async Task<IReadOnlyList<AccountVoucherDto>> ListMineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var owned = await vouchers.ListOwnedAsync(userId, cancellationToken);
        var result = new List<AccountVoucherDto>(owned.Count);
        foreach (var voucher in owned)
        {
            result.Add(voucher.ToAccount(clock, await vouchers.UsedByUserAsync(voucher.Id, userId, cancellationToken)));
        }

        return result;
    }

    // BR-LOY-05: khóa khách rồi mới kiểm và trừ điểm — gửi nhiều lần cùng lúc cũng chỉ tạo đúng số voucher đủ điểm
    public Task<Result<AccountVoucherDto>> RedeemPointsAsync(Guid userId, CancellationToken cancellationToken) =>
        unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (await loyalty.LockUserAsync(userId, ct) is not { IsDeleted: false } user)
            {
                return Result.Failure<AccountVoucherDto>(ErrorCodes.NotFound);
            }

            if (user.CurrentPoints < loyaltySettings.RedeemRequiredPoints)
            {
                return Result.Failure<AccountVoucherDto>(ErrorCodes.PointsInsufficient);
            }

            var voucher = await NewRedeemVoucherAsync(userId, ct);
            vouchers.Add(voucher);
            await ledger.RedeemAsync(user, loyaltySettings.RedeemRequiredPoints, voucher.Code, ct);
            emails.EnqueueRedeemed(user, voucher, loyaltySettings.RedeemRequiredPoints);
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(voucher.ToAccount(clock, usedByUser: 0));
        }, cancellationToken);

    // BR-LOY-07: cố định, một lần dùng, của riêng khách, hạn redeemExpiryDays ngày (hết hạn cuối ngày, giờ VN)
    private async Task<Voucher> NewRedeemVoucherAsync(Guid userId, CancellationToken cancellationToken)
    {
        string code;
        do
        {
            code = VoucherCodes.New(RedeemPrefix);
        }
        while ((await vouchers.ExistingCodesAsync([code], cancellationToken)).Count > 0);

        var now = clock.UtcNow;
        return new Voucher
        {
            Code = code,
            DiscountType = DiscountType.Fixed,
            DiscountValue = loyaltySettings.RedeemVoucherValue,
            MaxUsageTotal = 1,
            MaxUsagePerUser = 1,
            OwnerUserId = userId,
            RequireLogin = true,
            Source = VoucherSource.Redeem,
            StartUtc = now,
            EndUtc = clock.ToUtc(clock.TodayLocal.AddDays(settings.RedeemExpiryDays + 1), TimeOnly.MinValue),
            CreatedAtUtc = now,
        };
    }
}
