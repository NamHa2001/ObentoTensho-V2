using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Orders.Services;

// Áp một chuyển trạng thái lên đơn đang được theo dõi, trong transaction của bên gọi:
// máy trạng thái (BR-ORD-06), gọi xác nhận trước khi làm (BR-ORD-21), chỉ giao món khi đã thanh toán (BR-ORD-10),
// lịch sử, thư cho khách. Hoàn voucher (BR-ORD-07) và cộng/thu hồi điểm (BR-ORD-08, BR-ORD-15) nối vào ở các phần sau.
public sealed class OrderTransitions(IOrderRepository orders, IOrderEmailOutbox emails, IClock clock)
{
    public Result Apply(Order order, OrderStatus to, OrderActor actor, Guid? actorId, string? reason)
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
        return Result.Success();
    }

    private static Result? CheckAllowed(Order order, OrderStatus to, OrderActor actor, string? reason)
    {
        if (!OrderStateMachine.CanTransition(order.Status, to, actor)
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

    private void Notify(Order order, OrderStatus to, OrderActor actor, string? reason)
    {
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
