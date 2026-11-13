using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Delivery;
using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Delivery.Services;
using ObentoTensho.Core.Invoices.Services;
using ObentoTensho.Core.Customers.Services;
using ObentoTensho.Core.Loyalty.Services;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Realtime.Abstractions;
using ObentoTensho.Core.Realtime.Domain;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Vouchers.Domain;
using ObentoTensho.Core.Vouchers.Services;

namespace ObentoTensho.Core.Orders.Services;

// FR-ORD-01 (đặt món), FR-ORD-04 (theo dõi), BR-ORD-17 (khách tự hủy). Đặt món cần tài khoản đã xác minh SĐT
// (chủ dự án 2026-11-08); theo dõi/hủy bằng mã + token trong thư vẫn mở.
public sealed class PublicOrderService(
    IOrderRepository orders,
    ContactVerificationPolicy contactVerification,
    OrderCart cart,
    OrderPickup pickup,
    OrderTransitions transitions,
    IOrderEmailOutbox emails,
    ISecureTokenService secureTokens,
    IActionThrottle throttle,
    IOrderSettings settings,
    LoyaltyService loyalty,
    VoucherEvaluator vouchers,
    InvoiceService invoices,
    DeliveryQuoteService delivery,
    IUnitOfWork unitOfWork,
    IRealtimeEvents realtime,
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

    public async Task<Result<CreatedOrderDto>> CreateAsync(CreateOrderRequest request, Guid userId, CancellationToken cancellationToken)
    {
        if (await contactVerification.CheckAsync(userId, cancellationToken) is { } notVerified)
        {
            return Result.Failure<CreatedOrderDto>(notVerified);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<CreatedOrderDto>();
        }

        if (request.Invoice is not null && invoices.Validate(request.Invoice) is { Count: > 0 } invoiceErrors)
        {
            return Result.Failure<CreatedOrderDto>(ErrorCodes.ValidationFailed, invoiceErrors);
        }

        var shipping = await ShippingAsync(request, cancellationToken);
        if (!shipping.IsSuccess)
        {
            return Result.Failure<CreatedOrderDto>(shipping.ErrorCode ?? ErrorCodes.DeliveryUnavailable, shipping.FieldErrors);
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

        var pricing = await TotalsAsync(request.VoucherCode, priced.SubTotal, userId, cancellationToken);
        if (!pricing.IsSuccess)
        {
            return Result.Failure<CreatedOrderDto>(pricing.ErrorCode ?? ErrorCodes.VoucherInvalid);
        }

        var (totals, voucher) = pricing.Value;
        var needsCall = await NeedsCallAsync(phone, request.PaymentMethod, totals.TotalAmount + (shipping.Value.Quote?.Fee ?? 0), cancellationToken);
        var draft = new OrderDraft(request, phone, language, pickupAt.Value, priced.Items, totals, voucher, needsCall, userId, shipping.Value.Quote);
        return await PersistAsync(draft, cancellationToken);
    }

    private Task<Result<CreatedOrderDto>> PersistAsync(OrderDraft draft, CancellationToken cancellationToken) =>
        unitOfWork.TryInTransactionAsync(async ct =>
        {
            // BR-VCH-07: giữ lượt voucher cùng transaction với đơn — tạo đơn lỗi thì lượt không bị trừ
            if (draft.Voucher is { } voucher && !await vouchers.ReserveAsync(voucher, draft.UserId, ct))
            {
                return Result.Failure<CreatedOrderDto>(ErrorCodes.VoucherExhausted);
            }

            var token = secureTokens.Create();
            var order = NewOrder(draft, await orders.NextDailySeqAsync(clock.TodayLocal, ct), token.Hash);
            orders.Add(order);
            if (draft.Request.Invoice is { } invoice)
            {
                invoices.Attach(order.Id, invoice, staffId: null);
            }

            orders.AddHistory(new OrderStatusHistory
            {
                OrderId = order.Id,
                ToStatus = OrderStatus.Pending,
                ChangedByType = OrderActor.Customer,
                ChangedById = draft.UserId,
                ChangedAtUtc = order.CreatedAtUtc,
            });
            emails.EnqueueReceived(order, token.Value);
            realtime.Stage(RealtimeMessages.OrderCreated(order));
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(new CreatedOrderDto(order.ToPublic(clock), token.Value));
        }, cancellationToken);

    // Giao hàng: phí ship tính lại ở server từ địa chỉ (không tin số client gửi); đơn mang về thì không có
    private async Task<Result<Shipping>> ShippingAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Channel != OrderChannel.Delivery)
        {
            return Result.Success(new Shipping(null));
        }

        var quote = await delivery.QuoteAsync(request.DeliveryAddress, cancellationToken);
        return quote.IsSuccess
            ? Result.Success(new Shipping(quote.Value))
            : Result.Failure<Shipping>(
                quote.ErrorCode ?? ErrorCodes.DeliveryUnavailable,
                quote.FieldErrors.ToDictionary(e => string.Equals(e.Key, "address", StringComparison.Ordinal) ? "deliveryAddress" : e.Key, e => e.Value, StringComparer.Ordinal));
    }

    // BR-ORD-03, BR-VCH-04: có mã ⇒ giảm theo voucher (không giảm theo hạng); không mã ⇒ thành viên giảm theo hạng
    private async Task<Result<(OrderPricing.Totals Totals, Voucher? Voucher)>> TotalsAsync(string? voucherCode, decimal subTotal, Guid? userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(voucherCode))
        {
            var rate = await loyalty.MemberDiscountRateAsync(userId, cancellationToken);
            return Result.Success<(OrderPricing.Totals, Voucher?)>((OrderPricing.Compute(subTotal, rate, voucherDiscount: 0), null));
        }

        var evaluated = await vouchers.EvaluateAsync(voucherCode, subTotal, userId, cancellationToken);
        return evaluated.IsSuccess
            ? Result.Success<(OrderPricing.Totals, Voucher?)>((OrderPricing.Compute(subTotal, memberRate: 0, evaluated.Value.Discount), evaluated.Value.Voucher))
            : Result.Failure<(OrderPricing.Totals, Voucher?)>(evaluated.ErrorCode ?? ErrorCodes.VoucherInvalid);
    }

    // BR-ORD-21: khách lần đầu, đơn tiền mặt lớn, hoặc SĐT nhiều lần không đến lấy (BR-ORD-18) phải gọi xác nhận trước khi bếp làm
    private async Task<bool> NeedsCallAsync(string phone, PaymentMethod method, decimal total, CancellationToken cancellationToken) =>
        !await orders.HasCompletedForPhoneAsync(phone, cancellationToken)
        || (method == PaymentMethod.Cash && total >= settings.CallConfirmThreshold)
        || (await orders.FlaggedPhonesAsync([phone], clock.UtcNow - NoPickupWindow, settings.NoPickupFlagCount, cancellationToken)).Count > 0;

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
            TotalAmount = draft.Totals.TotalAmount + (draft.Shipping?.Fee ?? 0),
            Channel = draft.Shipping is null ? OrderChannel.Pickup : OrderChannel.Delivery,
            DeliveryAddress = draft.Shipping?.Address,
            DeliveryDistanceKm = draft.Shipping?.DistanceKm,
            DeliveryFee = draft.Shipping?.Fee ?? 0,
            VoucherId = draft.Voucher?.Id,
            VoucherCodeSnapshot = draft.Voucher?.Code,
            PaymentMethod = draft.Request.PaymentMethod,
            NeedsContactConfirm = draft.NeedsContactConfirm,
            CreatedAtUtc = clock.UtcNow,
            Items = draft.Items.ToList(),
        };

    // Sai mã và sai SĐT trả cùng NOT_FOUND để không dò được mã nào tồn tại
    public async Task<Result<PublicOrderDto>> LookupAsync(LookupOrderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Public(await FindWithSecretAsync(request.Code, o => SameSecret(PiiMask.Last4(o.CustomerPhone), request.PhoneLast4 ?? string.Empty), cancellationToken));
    }

    public async Task<Result<PublicOrderDto>> TrackAsync(TrackOrderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Public(await FindWithSecretAsync(request.Code, o => HasToken(o, request.Token), cancellationToken));
    }

    // Kênh realtime (RT-03): cùng điều kiện và cùng giới hạn sai với TrackAsync, chỉ trả Id để vào nhóm order:{id}
    public async Task<Result<Guid>> FindTrackedIdAsync(TrackOrderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var found = await FindWithSecretAsync(request.Code, o => HasToken(o, request.Token), cancellationToken);
        return found.IsSuccess ? Result.Success(found.Value.Id) : Result.Failure<Guid>(found.ErrorCode ?? ErrorCodes.NotFound);
    }

    public async Task<Result<Guid>> FindMineIdAsync(Guid userId, string code, CancellationToken cancellationToken) =>
        await orders.FindByCodeAsync(OrderCodes.Normalize(code), withItems: false, cancellationToken) is { } order && order.UserId == userId
            ? Result.Success(order.Id)
            : Result.Failure<Guid>(ErrorCodes.NotFound);

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

    private async Task<Result<Order>> FindWithSecretAsync(string? rawCode, Func<Order, bool> matches, CancellationToken cancellationToken)
    {
        var code = OrderCodes.Normalize(rawCode);
        var key = $"order-lookup:{code}";
        if (code.Length == 0 || throttle.Count(key) >= MaxLookupFailures)
        {
            return Result.Failure<Order>(code.Length == 0 ? ErrorCodes.NotFound : ErrorCodes.RateLimited);
        }

        var order = await orders.FindByCodeAsync(code, withItems: true, cancellationToken);
        if (order is null || !matches(order))
        {
            throttle.Increment(key, LookupLockout);
            return Result.Failure<Order>(ErrorCodes.NotFound);
        }

        return Result.Success(order);
    }

    private Result<PublicOrderDto> Public(Result<Order> found) =>
        found.IsSuccess ? Result.Success(found.Value.ToPublic(clock)) : Result.Failure<PublicOrderDto>(found.ErrorCode ?? ErrorCodes.NotFound);

    private bool HasToken(Order order, string? token) =>
        !string.IsNullOrEmpty(token) && secureTokens.HashEquals(order.AccessTokenHash, secureTokens.Hash(token));

    private static OrderLineDto Preview(OrderItem item) => item.ToLineDto(localized: true);

    private static string Language(string? lang) =>
        lang is not null && AuthPolicy.SupportedLanguages.Contains(lang, StringComparer.Ordinal) ? lang : AuthPolicy.DefaultLanguage;

    private static bool SameSecret(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(actual));

    // Phí ship của đơn giao hàng; đơn mang về thì Quote = null
    private sealed record Shipping(DeliveryQuoteDto? Quote);

    private sealed record OrderDraft(
        CreateOrderRequest Request,
        string Phone,
        string Language,
        DateTime PickupAtUtc,
        IReadOnlyList<OrderItem> Items,
        OrderPricing.Totals Totals,
        Voucher? Voucher,
        bool NeedsContactConfirm,
        Guid? UserId,
        DeliveryQuoteDto? Shipping);
}
