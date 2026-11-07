using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Orders.Domain;

// docs/srs/20 #30: đơn cần quản lý/thu ngân để ý. Ngưỡng: chờ xác nhận quá pendingAlertMinutes hoặc còn ít hơn chừng ấy
// tới giờ lấy; sẵn sàng quá noPickupMinutes sau giờ lấy (BR-ORD-18); đã thu tiền rồi hủy mà chưa ghi hoàn tiền (BR-ORD-16)
public static class OrderAttention
{
    public enum Kind
    {
        PendingOverdue,
        NotPickedUp,
        RefundPending,
    }

    public static Kind? Classify(Order order, DateTime nowUtc, int pendingAlertMinutes, int noPickupMinutes)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (order.RefundPending)
        {
            return Kind.RefundPending;
        }

        return order.Status switch
        {
            OrderStatus.Pending when order.CreatedAtUtc <= nowUtc.AddMinutes(-pendingAlertMinutes)
                || order.PickupAtUtc <= nowUtc.AddMinutes(pendingAlertMinutes) => Kind.PendingOverdue,
            OrderStatus.Ready when order.PickupAtUtc.AddMinutes(noPickupMinutes) <= nowUtc => Kind.NotPickedUp,
            _ => null,
        };
    }
}
