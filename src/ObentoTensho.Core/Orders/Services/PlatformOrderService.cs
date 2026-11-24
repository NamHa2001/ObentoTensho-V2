using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Contracts.Dtos.PlatformOrders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Realtime.Abstractions;
using ObentoTensho.Core.Realtime.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Orders.Services;

// docs/srs/23: đơn khách đặt trên app giao đồ ăn, thu ngân nhập lại từng món. Đơn đã thu qua app nên tạo ra là đã thanh toán;
// món vào bếp như đơn tại quầy, ra hết món thì đơn tự hoàn thành và chụp doanh thu ròng, thực nhận (BR-PLT-04).
public sealed class PlatformOrderService(
    IOrderRepository orders,
    OrderCart cart,
    OrderTransitions transitions,
    IOrderSettings settings,
    ISecureTokenService secureTokens,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IRealtimeEvents realtime,
    IClock clock)
{
    public const int CodeMaxLength = 50;
    public const int MaxLines = 30;
    private const string Entity = "order";
    private const string Language = "vi";

    // BR-PLT-01: kênh con hợp lệ theo từng nhóm; Xanh là app riêng
    private static readonly Dictionary<PaymentGroup, Platform[]> Channels = new()
    {
        [PaymentGroup.Tensho] = [Platform.Grab, Platform.Shopee],
        [PaymentGroup.Hima] = [Platform.Grab, Platform.Shopee, Platform.Be],
        [PaymentGroup.Xanh] = [Platform.Xanh],
    };

    public static bool IsValidChannel(PaymentGroup group, Platform platform) =>
        Channels.TryGetValue(group, out var allowed) && allowed.Contains(platform);

    public RevenueRatesDto Rates()
    {
        var rates = settings.RevenueRates;
        return new RevenueRatesDto(rates.DeductionPercent, rates.VatPercent, rates.PitPercent);
    }

    // Đơn app đang mở (bếp chưa ra hết món), mới nhất trước
    public async Task<IReadOnlyList<PlatformOrderDto>> OpenAsync(CancellationToken cancellationToken) =>
        (await orders.ListOpenDineInAsync(cancellationToken))
            .Where(o => o.Channel == OrderChannel.Platform)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(ToDto)
            .ToArray();

    public async Task<Result<PlatformOrderDto>> CreateAsync(CreatePlatformOrderRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = ValidateHeader(request.PaymentGroup, request.Platform, request.ExternalOrderCode);
        if (request.Items is not { Count: > 0 and <= MaxLines })
        {
            errors["items"] = [FieldErrorCodes.OutOfRange];
        }

        if (request.Note is { Length: > DineInService.NoteMaxLength })
        {
            errors["note"] = [FieldErrorCodes.TooLong];
        }

        if (errors.Count > 0)
        {
            return Result.Failure<PlatformOrderDto>(ErrorCodes.ValidationFailed, errors);
        }

        var code = NormalizeCode(request.ExternalOrderCode);
        if (await orders.ExternalCodeTakenAsync(request.PaymentGroup, request.Platform, code, exceptOrderId: null, cancellationToken))
        {
            return Duplicate<PlatformOrderDto>();
        }

        var now = clock.UtcNow;
        var priced = await cart.PriceAsync(request.Items, Language, cancellationToken, new OrderCart.DineInRound(1, now));
        if (!priced.IsValid)
        {
            return Result.Failure<PlatformOrderDto>(ErrorCodes.ItemUnavailable, priced.Lines
                .Where(l => l.ErrorCode is not null)
                .ToDictionary(l => $"items[{l.Index}]", l => new[] { l.ErrorCode! }, StringComparer.Ordinal));
        }

        var subTotal = priced.Items.Sum(i => i.LineTotal);
        if (Discount(subTotal, request.DiscountPercent, request.DiscountAmount) is not { } discount)
        {
            return Result.Failure<PlatformOrderDto>(ErrorCodes.ValidationFailed, Field(request.DiscountAmount is null ? "discountPercent" : "discountAmount", FieldErrorCodes.OutOfRange));
        }

        var order = NewOrder(request, code, await orders.NextDailySeqAsync(clock.TodayLocal, cancellationToken), staffId, now);
        foreach (var item in priced.Items)
        {
            order.Items.Add(item);
        }

        Price(order, subTotal, discount);
        orders.Add(order);
        orders.AddHistory(new OrderStatusHistory { OrderId = order.Id, ToStatus = OrderStatus.Preparing, ChangedByType = OrderActor.Staff, ChangedById = staffId, ChangedAtUtc = now });
        audit.Record(new AuditEntry("orders.platform_order.created", Entity, order.Id.ToString(), After: new { order.PaymentGroup, order.Platform, order.ExternalOrderCode, order.SubTotal, order.PlatformDiscount }));
        realtime.Stage(RealtimeMessages.OrderCreated(order));
        var saved = await unitOfWork.TrySaveAsync(ErrorCodes.ValidationFailed, cancellationToken);
        return saved.IsSuccess ? Result.Success(ToDto(order)) : Duplicate<PlatformOrderDto>();
    }

    // Sửa mã đơn hoặc giảm giá; đơn đã hoàn thành tính lại theo tỉ lệ đã chụp, không theo tỉ lệ hiện tại (BR-PLT-04)
    public async Task<Result<PlatformOrderDto>> UpdateAsync(Guid id, UpdatePlatformOrderRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await orders.FindPlatformOrderAsync(id, cancellationToken) is not { } order || order.Status == OrderStatus.Cancelled)
        {
            return Result.Failure<PlatformOrderDto>(ErrorCodes.NotFound);
        }

        var errors = ValidateHeader(order.PaymentGroup, order.Platform!.Value, request.ExternalOrderCode);
        if (errors.Count > 0)
        {
            return Result.Failure<PlatformOrderDto>(ErrorCodes.ValidationFailed, errors);
        }

        var code = NormalizeCode(request.ExternalOrderCode);
        if (await orders.ExternalCodeTakenAsync(order.PaymentGroup, order.Platform!.Value, code, order.Id, cancellationToken))
        {
            return Duplicate<PlatformOrderDto>();
        }

        if (Discount(order.SubTotal, request.DiscountPercent, request.DiscountAmount) is not { } discount)
        {
            return Result.Failure<PlatformOrderDto>(ErrorCodes.ValidationFailed, Field(request.DiscountAmount is null ? "discountPercent" : "discountAmount", FieldErrorCodes.OutOfRange));
        }

        var before = new { order.ExternalOrderCode, order.PlatformDiscount, order.Revenue?.ReceivedAmount };
        order.ExternalOrderCode = code;
        Price(order, order.SubTotal, discount);
        if (order.Revenue is { } snapshot)
        {
            order.Revenue = RevenueSplit.Compute(RevenueSplit.NetOf(order), new RevenueSplit.Rates(snapshot.DeductionPercent, snapshot.VatPercent, snapshot.PitPercent));
        }

        order.UpdatedAtUtc = clock.UtcNow;
        audit.Record(new AuditEntry("orders.platform_order.updated", Entity, order.Id.ToString(), Before: before, After: new { order.ExternalOrderCode, order.PlatformDiscount, order.Revenue?.ReceivedAmount }));
        realtime.Stage(RealtimeMessages.OrderChanged(order));
        var saved = await unitOfWork.TrySaveAsync(ErrorCodes.ValidationFailed, cancellationToken);
        return saved.IsSuccess ? Result.Success(ToDto(order)) : Duplicate<PlatformOrderDto>();
    }

    // Hủy khi bếp chưa xong; không tính doanh thu (BR-PLT-09), không có hoàn tiền phía quán vì tiền do app giữ
    public async Task<Result> CancelAsync(Guid id, string? reason, Guid staffId, CancellationToken cancellationToken)
    {
        var clean = reason?.Trim();
        if (string.IsNullOrEmpty(clean) || clean.Length > DineInService.ReasonMaxLength)
        {
            return Result.Failure(ErrorCodes.ValidationFailed, Field("reason", string.IsNullOrEmpty(clean) ? FieldErrorCodes.Required : FieldErrorCodes.TooLong));
        }

        if (await orders.FindPlatformOrderAsync(id, cancellationToken) is not { } order)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        var cancelled = await transitions.ApplyAsync(order, OrderStatus.Cancelled, OrderActor.Staff, staffId, clean, cancellationToken);
        if (!cancelled.IsSuccess)
        {
            return cancelled;
        }

        // App hoàn tiền cho khách: ghi đã hoàn để báo cáo doanh thu theo ngày thanh toán trừ lại khoản đã ghi thu
        order.RefundedAtUtc = order.CancelledAtUtc ?? clock.UtcNow;
        order.RefundedByStaffId = staffId;
        order.RefundNote = "App hoàn tiền cho khách";
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static Dictionary<string, string[]> ValidateHeader(PaymentGroup group, Platform platform, string? externalCode)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (group == PaymentGroup.InStore || !Enum.IsDefined(group))
        {
            errors["paymentGroup"] = [FieldErrorCodes.OutOfRange];
        }
        else if (!IsValidChannel(group, platform))
        {
            errors["platform"] = [FieldErrorCodes.OutOfRange];
        }

        var code = externalCode?.Trim() ?? string.Empty;
        if (code.Length == 0)
        {
            errors["externalOrderCode"] = [FieldErrorCodes.Required];
        }
        else if (code.Length > CodeMaxLength || code.IndexOfAny(['<', '>']) >= 0)
        {
            errors["externalOrderCode"] = [FieldErrorCodes.InvalidFormat];
        }

        return errors;
    }

    // BR-PLT-06: không phân biệt hoa thường, bỏ khoảng trắng đầu cuối
    private static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    // BR-PLT-07: % 0–100 ⇒ số tiền = giá gốc × % (làm tròn đồng); hoặc số tiền 0..giá gốc ⇒ % hiển thị một chữ số lẻ
    public static (decimal Amount, decimal? Percent)? Discount(decimal subTotal, decimal? percent, decimal? amount)
    {
        if (amount is { } money)
        {
            if (money < 0 || money > subTotal || decimal.Round(money) != money)
            {
                return null;
            }

            return (money, subTotal == 0 ? 0 : decimal.Round(money * 100m / subTotal, 1, MidpointRounding.AwayFromZero));
        }

        var rate = percent ?? 0;
        if (rate is < 0 or > 100 || decimal.Round(rate, 2) != rate)
        {
            return null;
        }

        return (decimal.Round(subTotal * rate / 100m, 0, MidpointRounding.AwayFromZero), rate);
    }

    private static void Price(Order order, decimal subTotal, (decimal Amount, decimal? Percent) discount)
    {
        order.SubTotal = subTotal;
        order.PlatformDiscount = discount.Amount;
        order.PlatformDiscountPercent = discount.Percent;
        order.TotalAmount = subTotal - discount.Amount;
    }

    private Order NewOrder(CreatePlatformOrderRequest request, string code, int dailySeq, Guid staffId, DateTime now) =>
        new()
        {
            OrderCode = OrderCodes.New(),
            DailySeq = dailySeq,
            AccessTokenHash = secureTokens.Create().Hash,
            CustomerName = $"{request.Platform} {request.PaymentGroup} · {code}",
            CustomerPhone = string.Empty,
            CustomerEmail = string.Empty,
            Lang = Language,
            PickupAtUtc = now,
            Channel = OrderChannel.Platform,
            PaymentGroup = request.PaymentGroup,
            Platform = request.Platform,
            ExternalOrderCode = code,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            SubTotal = 0,
            TotalAmount = 0,

            // Tiền khách trả cho app, app chuyển về tài khoản quán: ghi là chuyển khoản, đã thanh toán
            PaymentMethod = PaymentMethod.BankTransfer,
            PaymentStatus = PaymentStatus.Paid,
            PaidAtUtc = now,
            PaidByStaffId = staffId,
            Status = OrderStatus.Preparing,
            CreatedAtUtc = now,
        };

    private PlatformOrderDto ToDto(Order order)
    {
        var split = order.Revenue ?? RevenueSplit.Compute(RevenueSplit.NetOf(order), settings.RevenueRates);
        return new PlatformOrderDto(
            order.Id,
            order.OrderCode,
            order.PaymentGroup,
            order.Platform ?? Platform.Grab,
            order.ExternalOrderCode ?? string.Empty,
            order.Status,
            order.CreatedAtUtc,
            order.Items.Count(i => !i.IsVoided && i.ServedAtUtc is null),
            order.SubTotal,
            order.PlatformDiscountPercent,
            order.PlatformDiscount,
            split.NetRevenue,
            split.DeductionAmount,
            split.VatAmount,
            split.PitAmount,
            split.ReceivedAmount);
    }

    private static Result<T> Duplicate<T>() =>
        Result.Failure<T>(ErrorCodes.ValidationFailed, Field("externalOrderCode", FieldErrorCodes.Duplicate));

    private static Dictionary<string, string[]> Field(string name, string code) =>
        new(StringComparer.Ordinal) { [name] = [code] };
}
