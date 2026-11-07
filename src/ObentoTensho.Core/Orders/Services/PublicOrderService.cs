using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Loyalty.Services;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Orders.Services;

// FR-ORD-01 (đặt món), FR-ORD-04 (theo dõi), BR-ORD-17 (khách tự hủy). Không cần tài khoản; đang đăng nhập thì gắn vào tài khoản.
public sealed class PublicOrderService(
    IOrderRepository orders,
    OrderCart cart,
    OrderPickup pickup,
    OrderTransitions transitions,
    IOrderEmailOutbox emails,
    ISecureTokenService secureTokens,
    IActionThrottle throttle,
    IOrderSettings settings,
    LoyaltyService loyalty,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<CreateOrderRequest> validator,
    IValidator<QuoteOrderRequest> quoteValidator)
{
    public const int MaxPageSize = 50;

    // SEC-01 e: sai 5 lần với một mã ⇒ khóa tra cứu mã đó 15 phút (giới hạn theo IP nằm ở rate limit "lookup")
    private const int MaxLookupFailures = 5;
    private static readonly TimeSpan NoPickupWindow = TimeSpan.FromDays(90);
    private static readonly TimeSpan LookupLockout = TimeSpan.FromMinutes(15);

    // Mở giỏ ⇒ kiểm lại giá/tình trạng (docs/srs/12 §4.1 bước 1); không tạo gì
    public async Task<Result<OrderQuoteDto>> QuoteAsync(QuoteOrderRequest request, Guid? userId, CancellationToken cancellationToken)
    {
        var validation = await quoteValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<OrderQuoteDto>();
        }

        var priced = await cart.PriceAsync(request.Items, Language(request.Lang), cancellationToken);
        var totals = OrderPricing.Compute(priced.SubTotal, await loyalty.MemberDiscountRateAsync(userId, cancellationToken), voucherDiscount: 0);
        return Result.Success(new OrderQuoteDto(
            priced.IsValid,
            priced.Lines.Select(l => new QuoteLineDto(l.Index, l.Item is { } item ? Preview(item) : null, l.ErrorCode)).ToArray(),
            totals.SubTotal,
            totals.MemberDiscount,
            totals.VoucherDiscount,
            totals.TotalAmount));
    }

    public Task<PickupTimesDto> PickupTimesAsync(DateOnly date, CancellationToken cancellationToken) =>
        pickup.TimesAsync(date, cancellationToken);

    public async Task<Result<CreatedOrderDto>> CreateAsync(CreateOrderRequest request, Guid? userId, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<CreatedOrderDto>();
        }

        // Validator đã kiểm định dạng; nhánh này chỉ để có giá trị chuẩn hóa
        if (!VietnamesePhoneNumber.TryNormalize(request.CustomerPhone, out var phone))
        {
            return Result.Failure<CreatedOrderDto>(ErrorCodes.ValidationFailed);
        }

        var pickupAt = await pickup.CheckAsync(request.PickupDate, request.PickupTime, cancellationToken);
        if (!pickupAt.IsSuccess)
        {
            return Result.Failure<CreatedOrderDto>(pickupAt.ErrorCode ?? ErrorCodes.DateOutOfRange);
        }

        var language = Language(request.Lang);
        var priced = await cart.PriceAsync(request.Items, language, cancellationToken);
        if (!priced.IsValid)
        {
            return Result.Failure<CreatedOrderDto>(ErrorCodes.ItemUnavailable, priced.Lines
                .Where(l => l.ErrorCode is not null)
                .ToDictionary(l => $"items[{l.Index}]", l => new[] { l.ErrorCode! }, StringComparer.Ordinal));
        }

        if (await orders.CountOpenForPhoneAsync(phone, cancellationToken) >= settings.MaxActivePerPhone)
        {
            return Result.Failure<CreatedOrderDto>(ErrorCodes.OrderLimit);
        }

        // BR-ORD-03: thành viên được giảm theo hạng (khi có voucher thì hai loại giảm loại trừ nhau — phần voucher)
        var totals = OrderPricing.Compute(priced.SubTotal, await loyalty.MemberDiscountRateAsync(userId, cancellationToken), voucherDiscount: 0);

        // BR-ORD-21: khách lần đầu, đơn tiền mặt lớn, hoặc SĐT nhiều lần không đến lấy (BR-ORD-18) phải gọi xác nhận trước khi bếp làm
        var needsCall = !await orders.HasCompletedForPhoneAsync(phone, cancellationToken)
            || (request.PaymentMethod == PaymentMethod.Cash && totals.TotalAmount >= settings.CallConfirmThreshold)
            || (await orders.FlaggedPhonesAsync([phone], clock.UtcNow - NoPickupWindow, settings.NoPickupFlagCount, cancellationToken)).Count > 0;

        var draft = new OrderDraft(request, phone, language, pickupAt.Value, priced.Items, totals, needsCall, userId);
        return await unitOfWork.TryInTransactionAsync(async ct =>
        {
            var token = secureTokens.Create();
            var order = NewOrder(draft, await orders.NextDailySeqAsync(clock.TodayLocal, ct), token.Hash);
            orders.Add(order);
            orders.AddHistory(new OrderStatusHistory
            {
                OrderId = order.Id,
                ToStatus = OrderStatus.Pending,
                ChangedByType = OrderActor.Customer,
                ChangedById = userId,
                ChangedAtUtc = order.CreatedAtUtc,
            });
            emails.EnqueueReceived(order, token.Value);
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(new CreatedOrderDto(order.ToPublic(clock), token.Value));
        }, cancellationToken);
    }

    private Order NewOrder(OrderDraft draft, int dailySeq, string tokenHash) =>
        new()
        {
            OrderCode = OrderCodes.New(),
            DailySeq = dailySeq,
            AccessTokenHash = tokenHash,
            UserId = draft.UserId,
            CustomerName = draft.Request.CustomerName.Trim(),
            CustomerPhone = draft.Phone,
            CustomerEmail = EmailAddress.Normalize(draft.Request.CustomerEmail),
            Lang = draft.Language,
            PickupAtUtc = draft.PickupAtUtc,
            Note = string.IsNullOrWhiteSpace(draft.Request.Note) ? null : draft.Request.Note.Trim(),
            SubTotal = draft.Totals.SubTotal,
            MemberDiscount = draft.Totals.MemberDiscount,
            VoucherDiscount = draft.Totals.VoucherDiscount,
            TotalAmount = draft.Totals.TotalAmount,
            PaymentMethod = draft.Request.PaymentMethod,
            NeedsContactConfirm = draft.NeedsContactConfirm,
            CreatedAtUtc = clock.UtcNow,
            Items = draft.Items.ToList(),
        };

    // Sai mã và sai SĐT trả cùng NOT_FOUND để không dò được mã nào tồn tại
    public async Task<Result<PublicOrderDto>> LookupAsync(LookupOrderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await FindWithSecretAsync(request.Code, o => SameSecret(PiiMask.Last4(o.CustomerPhone), request.PhoneLast4 ?? string.Empty), cancellationToken);
    }

    public async Task<Result<PublicOrderDto>> TrackAsync(TrackOrderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await FindWithSecretAsync(request.Code, o => HasToken(o, request.Token), cancellationToken);
    }

    public Task<Result<PublicOrderDto>> CancelAsync(string code, CancelOrderRequest request, Guid? userId, CancellationToken cancellationToken) =>
        unitOfWork.TryInTransactionAsync(async ct =>
        {
            var order = await orders.FindByCodeAsync(OrderCodes.Normalize(code), withItems: true, ct);
            if (order is null || !(HasToken(order, request.Token) || (userId is not null && order.UserId == userId)))
            {
                return Result.Failure<PublicOrderDto>(ErrorCodes.NotFound);
            }

            var applied = await transitions.ApplyAsync(order, OrderStatus.Cancelled, OrderActor.Customer, userId, request.Reason, ct);
            if (!applied.IsSuccess)
            {
                return Result.Failure<PublicOrderDto>(applied.ErrorCode ?? ErrorCodes.InvalidTransition, applied.FieldErrors);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(order.ToPublic(clock));
        }, cancellationToken);

    // Khách đã đăng nhập xem đơn của mình (docs/srs/12 §8 /account/orders)
    public async Task<PagedResponse<PublicOrderDto>> ListMineAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (items, total) = await orders.ListForUserAsync(userId, page, pageSize, cancellationToken);
        return new PagedResponse<PublicOrderDto>(items.Select(o => o.ToPublic(clock)).ToArray(), new PageMeta(page, pageSize, total));
    }

    public async Task<Result<PublicOrderDto>> GetMineAsync(Guid userId, string code, CancellationToken cancellationToken) =>
        await orders.FindByCodeAsync(OrderCodes.Normalize(code), withItems: true, cancellationToken) is { } order && order.UserId == userId
            ? Result.Success(order.ToPublic(clock))
            : Result.Failure<PublicOrderDto>(ErrorCodes.NotFound);

    private async Task<Result<PublicOrderDto>> FindWithSecretAsync(string? rawCode, Func<Order, bool> matches, CancellationToken cancellationToken)
    {
        var code = OrderCodes.Normalize(rawCode);
        var key = $"order-lookup:{code}";
        if (code.Length == 0 || throttle.Count(key) >= MaxLookupFailures)
        {
            return Result.Failure<PublicOrderDto>(code.Length == 0 ? ErrorCodes.NotFound : ErrorCodes.RateLimited);
        }

        var order = await orders.FindByCodeAsync(code, withItems: true, cancellationToken);
        if (order is null || !matches(order))
        {
            throttle.Increment(key, LookupLockout);
            return Result.Failure<PublicOrderDto>(ErrorCodes.NotFound);
        }

        return Result.Success(order.ToPublic(clock));
    }

    private bool HasToken(Order order, string? token) =>
        !string.IsNullOrEmpty(token) && secureTokens.HashEquals(order.AccessTokenHash, secureTokens.Hash(token));

    private static OrderLineDto Preview(OrderItem item) => item.ToLineDto(localized: true);

    private static string Language(string? lang) =>
        lang is not null && AuthPolicy.SupportedLanguages.Contains(lang, StringComparer.Ordinal) ? lang : AuthPolicy.DefaultLanguage;

    private static bool SameSecret(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(actual));

    private sealed record OrderDraft(
        CreateOrderRequest Request,
        string Phone,
        string Language,
        DateTime PickupAtUtc,
        IReadOnlyList<OrderItem> Items,
        OrderPricing.Totals Totals,
        bool NeedsContactConfirm,
        Guid? UserId);
}
