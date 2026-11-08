namespace ObentoTensho.Contracts.Realtime;

/// <summary>
/// Tên sự kiện SignalR cố định (docs/srs/06 §B.2). Payload chỉ có id/mã/trạng thái/số chỗ, không có tên, SĐT, email;
/// client cần chi tiết thì gọi REST. Mất kết nối rồi nối lại thì gọi REST lấy trạng thái mới nhất (RT-06).
/// </summary>
public static class RealtimeEventNames
{
    public const string ReservationCreated = "reservation.created";
    public const string ReservationStatusChanged = "reservation.statusChanged";
    public const string SlotAvailabilityChanged = "slot.availabilityChanged";
    public const string OrderCreated = "order.created";
    public const string OrderStatusChanged = "order.statusChanged";
    public const string ContactCreated = "contact.created";
    public const string TableStatusChanged = "table.statusChanged";
}
