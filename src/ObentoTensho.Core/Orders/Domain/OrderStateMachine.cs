using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Orders.Domain;

// BR-ORD-06, bảng trạng thái docs/srs/12 §6: chỉ các bước dưới đây hợp lệ; Completed và Cancelled là trạng thái cuối
public static class OrderStateMachine
{
    // Tại bàn không qua "Sẵn sàng" cả phiếu (bếp ra từng món): đang phục vụ ⇒ thanh toán xong là hoàn thành
    public static bool CanTransition(OrderStatus from, OrderStatus to, OrderActor actor, OrderChannel channel = OrderChannel.Pickup) => (from, to) switch
    {
        (OrderStatus.Preparing, OrderStatus.Completed) when channel == OrderChannel.DineIn => actor == OrderActor.Staff,
        (OrderStatus.Pending, OrderStatus.Preparing) => actor == OrderActor.Staff,
        (OrderStatus.Preparing, OrderStatus.Ready) => actor == OrderActor.Staff,
        (OrderStatus.Ready, OrderStatus.Completed) => actor == OrderActor.Staff,

        // BR-ORD-17: khách chỉ tự hủy khi đơn còn chờ; từ lúc bếp làm phải gọi quán
        (OrderStatus.Pending, OrderStatus.Cancelled) => true,
        (OrderStatus.Preparing or OrderStatus.Ready, OrderStatus.Cancelled) => actor != OrderActor.Customer,
        _ => false,
    };

    // Nhân viên hủy phải ghi lý do (gửi kèm trong thư cho khách)
    public static bool RequiresReason(OrderStatus to, OrderActor actor) =>
        to == OrderStatus.Cancelled && actor != OrderActor.Customer;

    // SEC-12: quyền theo bước — bếp làm món, thu ngân giao món, hủy theo quyền hủy
    public static StaffCapability RequiredCapability(OrderStatus to) => to switch
    {
        OrderStatus.Preparing or OrderStatus.Ready => StaffCapability.OrdersKitchen,
        OrderStatus.Completed => StaffCapability.OrdersCashier,
        _ => StaffCapability.OrdersCancel,
    };

    public static bool IsOpen(OrderStatus status) => status is OrderStatus.Pending or OrderStatus.Preparing or OrderStatus.Ready;
}
