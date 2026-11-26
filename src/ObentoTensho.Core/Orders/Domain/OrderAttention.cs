using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Orders.Domain;

// docs/srs/20 #30: đơn cần quản lý/thu ngân để ý. Ngưỡng: chờ xác nhận quá pendingAlertMinutes hoặc còn ít hơn chừng ấy
// tới giờ lấy; sẵn sàng quá noPickupMinutes sau giờ lấy (BR-ORD-18). Hoàn tiền không còn theo dõi ở đây: quản lý tự xử lý (chủ quán 2026-11-26)
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
        return order.Status switch
        {
            OrderStatus.Pending when order.CreatedAtUtc <= nowUtc.AddMinutes(-pendingAlertMinutes)
                || order.PickupAtUtc <= nowUtc.AddMinutes(pendingAlertMinutes) => Kind.PendingOverdue,
            OrderStatus.Ready when order.Channel == OrderChannel.Pickup && order.PickupAtUtc.AddMinutes(noPickupMinutes) <= nowUtc => Kind.NotPickedUp,
            _ => null,
        };
    }
}
