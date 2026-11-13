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
    IVoucherEmailOutbox emails,
    IActionThrottle throttle,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<ValidateVoucherRequest> validator,
    IValidator<VoucherOptionsRequest> optionsValidator)
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

        var fee = request.DeliveryFee ?? 0;
        var evaluated = await evaluator.EvaluateAsync(request.Code, priced.SubTotal, userId, cancellationToken, fee);
        if (!evaluated.IsSuccess)
        {
            return Result.Failure<VoucherCheckDto>(VoucherEvaluator.ErrorOrInvalid(evaluated));
        }

        // Mã giảm ship: tiền món giữ nguyên, phần giảm nằm ở ShippingDiscount; các tổng ở đây chưa gồm phí ship
        var shipping = evaluated.Value.Voucher.AppliesTo == VoucherTarget.Shipping;
        var withVoucher = OrderPricing.Compute(priced.SubTotal, memberRate: 0, shipping ? 0 : evaluated.Value.Discount);
        var shippingDiscount = shipping ? evaluated.Value.Discount : 0;
        var withMember = OrderPricing.Compute(priced.SubTotal, await loyaltyService.MemberDiscountRateAsync(userId, cancellationToken), voucherDiscount: 0);
        return Result.Success(new VoucherCheckDto(
            evaluated.Value.Voucher.Code,
            priced.SubTotal,
            withVoucher.VoucherDiscount,
            withVoucher.TotalAmount,
            withMember.MemberDiscount,
            withMember.TotalAmount,
            withMember.TotalAmount < withVoucher.TotalAmount - shippingDiscount,
            shippingDiscount,
            evaluated.Value.Voucher.AppliesTo));
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

    // Kiểu ví điện tử: khách thấy trước mình đổi được gì, tốn bao nhiêu điểm
    public async Task<IReadOnlyList<RedeemRewardDto>> ListRewardsAsync(CancellationToken cancellationToken) =>
        (await vouchers.ListRewardsAsync(activeOnly: true, cancellationToken))
            .Select(r => new RedeemRewardDto(r.Id, r.Name, r.Description, r.PointsCost, r.DiscountType, r.DiscountValue, r.MaxDiscountAmount, r.MinOrderValue, r.ValidDays))
            .ToArray();

    // BR-LOY-05: khóa khách rồi mới kiểm và trừ điểm — gửi nhiều lần cùng lúc cũng chỉ tạo đúng số voucher đủ điểm
    public Task<Result<AccountVoucherDto>> RedeemPointsAsync(Guid userId, int rewardId, CancellationToken cancellationToken) =>
        unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (await vouchers.FindRewardAsync(rewardId, ct) is not { IsActive: true } reward)
            {
                return Result.Failure<AccountVoucherDto>(ErrorCodes.NotFound);
            }

            if (await loyalty.LockUserAsync(userId, ct) is not { IsDeleted: false } user)
            {
                return Result.Failure<AccountVoucherDto>(ErrorCodes.NotFound);
            }

            if (user.CurrentPoints < reward.PointsCost)
            {
                return Result.Failure<AccountVoucherDto>(ErrorCodes.PointsInsufficient);
            }

            var voucher = await NewRedeemVoucherAsync(userId, reward, ct);
            vouchers.Add(voucher);
            await ledger.RedeemAsync(user, reward.PointsCost, voucher.Code, ct);
            emails.EnqueueRedeemed(user, voucher, reward.PointsCost);
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(voucher.ToAccount(clock, usedByUser: 0));
        }, cancellationToken);

    // Hộp chọn mã ở bước thanh toán: mã riêng của khách và mã của khuyến mãi đang chạy, mỗi mã kèm kết quả kiểm với giỏ này.
    // Dùng đúng quy tắc của lúc đặt đơn (VoucherRules) nên mã báo "dùng được" thì đặt đơn cũng nhận
    public async Task<Result<IReadOnlyList<VoucherOptionDto>>> OptionsAsync(VoucherOptionsRequest request, Guid userId, CancellationToken cancellationToken)
    {
        var validation = await optionsValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<IReadOnlyList<VoucherOptionDto>>();
        }

        var priced = await cart.PriceAsync(request.Items, AuthPolicy.DefaultLanguage, cancellationToken);
        if (!priced.IsValid)
        {
            return Result.Failure<IReadOnlyList<VoucherOptionDto>>(ErrorCodes.ItemUnavailable);
        }

        var now = clock.UtcNow;
        var user = await loyalty.FindUserAsync(userId, cancellationToken);
        var owned = (await vouchers.ListOwnedAsync(userId, cancellationToken))
            .Where(v => v.EndUtc is not { } end || now < end)
            .Select(v => (Voucher: v, Title: (string?)null));
        var promoted = (await vouchers.ListPromotedAsync(clock.TodayLocal, cancellationToken)).Select(p => (p.Voucher, Title: (string?)p.Title));
        var options = new List<VoucherOptionDto>();
        foreach (var (voucher, title) in owned.Concat(promoted).DistinctBy(x => x.Voucher.Id))
        {
            var used = await vouchers.UsedByUserAsync(voucher.Id, userId, cancellationToken);
            var error = VoucherRules.Check(voucher, new VoucherRules.Context(now, priced.SubTotal, userId, user?.MemberTier, used, request.DeliveryFee ?? 0));

            // Mã đã dùng hết lượt của khách thì thôi không hiện, tránh danh sách đầy mã cũ
            if (string.Equals(error, ErrorCodes.VoucherExhausted, StringComparison.Ordinal) || string.Equals(error, ErrorCodes.VoucherInvalid, StringComparison.Ordinal))
            {
                continue;
            }

            var discount = error is null ? VoucherRules.Discount(voucher, priced.SubTotal, request.DeliveryFee ?? 0) : 0;
            options.Add(new VoucherOptionDto(
                voucher.Code,
                title,
                voucher.DiscountType,
                voucher.DiscountValue,
                voucher.MaxDiscountAmount,
                voucher.MinOrderValue,
                VoucherMappings.EndDate(voucher, clock),
                voucher.Source,
                error is null,
                discount,
                error,
                voucher.AppliesTo));
        }

        return Result.Success<IReadOnlyList<VoucherOptionDto>>(options.OrderByDescending(o => o.Usable).ThenByDescending(o => o.Discount).ThenBy(o => o.MinOrderValue).ToArray());
    }

    // BR-LOY-07: một lần dùng, của riêng khách, điều kiện chép từ phần thưởng lúc đổi; hết hạn cuối ngày (giờ VN)
    private async Task<Voucher> NewRedeemVoucherAsync(Guid userId, RedeemReward reward, CancellationToken cancellationToken)
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
            DiscountType = reward.DiscountType,
            DiscountValue = reward.DiscountValue,
            MaxDiscountAmount = reward.MaxDiscountAmount,
            MinOrderValue = reward.MinOrderValue,
            MaxUsageTotal = 1,
            MaxUsagePerUser = 1,
            OwnerUserId = userId,
            RequireLogin = true,
            Source = VoucherSource.Redeem,
            StartUtc = now,
            EndUtc = clock.ToUtc(clock.TodayLocal.AddDays(reward.ValidDays + 1), TimeOnly.MinValue),
            CreatedAtUtc = now,
        };
    }
}
