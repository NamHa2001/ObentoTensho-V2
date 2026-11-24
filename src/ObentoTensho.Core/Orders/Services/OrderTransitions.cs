using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Loyalty.Domain;
using ObentoTensho.Core.Loyalty.Services;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Realtime.Abstractions;
using ObentoTensho.Core.Realtime.Domain;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Vouchers.Services;

namespace ObentoTensho.Core.Orders.Services;

// Áp một chuyển trạng thái lên đơn đang được theo dõi, trong transaction của bên gọi:
// máy trạng thái (BR-ORD-06), gọi xác nhận trước khi làm (BR-ORD-21), chỉ giao món khi đã thanh toán (BR-ORD-10),
// lịch sử, thư cho khách, cộng/thu hồi điểm (BR-ORD-08, BR-LOY-10) và hoàn voucher (BR-ORD-07) cùng transaction.
public sealed class OrderTransitions(
    IOrderRepository orders,
    IOrderEmailOutbox emails,
    LoyaltyLedger ledger,
    VoucherEvaluator vouchers,
    ILoyaltySettings loyaltySettings,
    IOrderSettings orderSettings,
    IRealtimeEvents realtime,
    IClock clock)
{
    public async Task<Result> ApplyAsync(Order order, OrderStatus to, OrderActor actor, Guid? actorId, string? reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (CheckAllowed(order, to, actor, reason) is { } failure)
        {
            return failure;
        }

        var from = order.Status;
        var now = clock.UtcNow;
        var cleanReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        order.Status = to;
        order.UpdatedAtUtc = now;
        switch (to)
        {
            case OrderStatus.Completed:
                order.CompletedAtUtc = now;
                order.Revenue = RevenueSplit.Compute(RevenueSplit.NetOf(order), orderSettings.RevenueRates);
                break;
            case OrderStatus.Cancelled:
                order.CancelledAtUtc = now;
                order.CancelReason = cleanReason;
                order.CancelledByType = actor;
                break;
        }

        orders.AddHistory(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = from,
            ToStatus = to,
            ChangedByType = actor,
            ChangedById = actorId,
            Reason = cleanReason,
            ChangedAtUtc = now,
        });
        Notify(order, to, actor, cleanReason);
        realtime.Stage(RealtimeMessages.OrderChanged(order));
        await SettleLoyaltyAsync(order, to, cancellationToken);

        // BR-ORD-07: hủy đơn ⇒ hoàn đúng một lượt voucher (máy trạng thái chỉ cho hủy một lần)
        if (to == OrderStatus.Cancelled && order.VoucherId is { } voucherId)
        {
            await vouchers.ReleaseAsync(voucherId, order.UserId, cancellationToken);
        }
        return Result.Success();
    }

    // BR-ORD-08/13: điểm cộng đúng một lần khi đơn của thành viên hoàn thành (đã thanh toán — BR-ORD-10), tính trên tổng sau giảm;
    // đơn đã cộng mà bị hủy thì thu hồi (máy trạng thái hiện không cho hủy đơn đã hoàn thành — giữ cho an toàn)
    private async Task SettleLoyaltyAsync(Order order, OrderStatus to, CancellationToken cancellationToken)
    {
        if (order.UserId is not { } userId)
        {
            return;
        }

        if (to == OrderStatus.Completed)
        {
            // Tiền ship không tính điểm
            var points = LoyaltyRules.OrderPoints(order.TotalAmount - (order.DeliveryFee - order.ShippingDiscount), loyaltySettings.VndPerPoint);
            order.PointsEarned = await ledger.EarnAsync(userId, LoyaltyTransactionType.OrderEarn, order.OrderCode, points, note: null, cancellationToken);
        }
        else if (to == OrderStatus.Cancelled && order.PointsEarned > 0)
        {
            await ledger.RevokeAsync(userId, LoyaltyTransactionType.OrderEarn, order.OrderCode, cancellationToken);
        }
    }

    private static Result? CheckAllowed(Order order, OrderStatus to, OrderActor actor, string? reason)
    {
        if (!OrderStateMachine.CanTransition(order.Status, to, actor, order.Channel)
            || (actor == OrderActor.Customer && !order.CanCustomerCancel()))
        {
            return Result.Failure(ErrorCodes.InvalidTransition);
        }

        if (OrderStateMachine.RequiresReason(to, actor) && string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["reason"] = [FieldErrorCodes.Required],
            });
        }

        return to switch
        {
            OrderStatus.Preparing when !order.ReadyForKitchen => Result.Failure(ErrorCodes.ContactNotConfirmed),
            OrderStatus.Completed when order.PaymentStatus != PaymentStatus.Paid => Result.Failure(ErrorCodes.PaymentRequired),
            _ => null,
        };
    }

    // Tại bàn không gửi thư (khách đang ở quán, phiếu không có email)
    private void Notify(Order order, OrderStatus to, OrderActor actor, string? reason)
    {
        // Đơn giao hàng không báo "đến lấy" khi sẵn sàng
        if (order.Channel is OrderChannel.DineIn or OrderChannel.Counter || (order.Channel == OrderChannel.Delivery && to == OrderStatus.Ready))
        {
            return;
        }

        switch (to)
        {
            case OrderStatus.Ready:
                emails.EnqueueReady(order);
                break;
            case OrderStatus.Cancelled when actor == OrderActor.Customer:
                emails.EnqueueCancelledByCustomer(order);
                break;
            case OrderStatus.Cancelled:
                emails.EnqueueCancelled(order, reason ?? string.Empty);
                break;
        }
    }
}
