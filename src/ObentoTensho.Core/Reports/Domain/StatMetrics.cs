namespace ObentoTensho.Core.Reports.Domain;

// BR-RPT-31: một định nghĩa duy nhất cho mọi số liệu. Tiền thu ghi theo ngày thanh toán, hoàn tiền là khoản riêng theo ngày hoàn
public static class StatMetrics
{
    public const string RevenuePaid = "revenue_paid";
    public const string RefundsAmount = "refunds_amount";
    public const string OrdersCreated = "orders_created";
    public const string OrdersPaid = "orders_paid";
    public const string OrdersCompleted = "orders_completed";
    public const string OrdersCancelled = "orders_cancelled";
    public const string ItemQuantity = "item_quantity";
    public const string ItemRevenue = "item_revenue";
    public const string ReservationsTotal = "reservations_total";
    public const string ReservationsArrived = "reservations_arrived";
    public const string ReservationsCancelled = "reservations_cancelled";
    public const string ReservationsNoShow = "reservations_noshow";
    public const string ReservationGuests = "reservation_guests";
    public const string NewCustomers = "new_customers";

    // Tiền ship thực thu (phí ship trừ phần mã giảm ship) theo ngày thanh toán
    public const string ShippingCollected = "shipping_collected";

    public const string CategoryPrefix = "category:";
    public const string ItemPrefix = "item:";
    public const string HourPrefix = "hour:";
    public const string AreaPrefix = "area:";
    public const string StatusPrefix = "status:";
    public const string ChannelPrefix = "channel:";
}
