using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Orders.Domain;

// docs/srs/08 §5 Orders. Giờ lấy lưu UTC; ngày/giờ VN suy ra khi hiển thị. Tiền là VND nguyên (decimal(18,0)).
public sealed class Order
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string OrderCode { get; init; }

    public required int DailySeq { get; init; }

    public required string AccessTokenHash { get; init; }

    // BR-AUTH-17: đơn vãng lai có thể được gắn vào tài khoản sau
    public Guid? UserId { get; set; }

    // Tại bàn: tên/SĐT thành viên gắn lúc thanh toán (để tích điểm), còn lại để trống
    public required string CustomerName { get; set; }

    public required string CustomerPhone { get; set; }

    public required string CustomerEmail { get; init; }

    public required string Lang { get; init; }

    // Tại bàn: giờ mở bàn
    public required DateTime PickupAtUtc { get; init; }

    public OrderChannel Channel { get; init; } = OrderChannel.Pickup;

    // Tại bàn: bàn đang ngồi (đổi khi chuyển bàn) và số khách
    public int? TableId { get; set; }

    public int? GuestCount { get; set; }

    // Giao hàng: địa chỉ, quãng đường lái xe (km) và phí ship lúc đặt; phí ship đã gồm trong TotalAmount
    public string? DeliveryAddress { get; init; }

    public decimal? DeliveryDistanceKm { get; init; }

    public decimal DeliveryFee { get; init; }

    // Mã giảm phí ship: phần giảm trên phí ship (không vượt phí ship)
    public decimal ShippingDiscount { get; init; }

    public string? Note { get; init; }

    public required decimal SubTotal { get; set; }

    public decimal MemberDiscount { get; set; }

    public decimal VoucherDiscount { get; set; }

    public required decimal TotalAmount { get; set; }

    public int? VoucherId { get; set; }

    public string? VoucherCodeSnapshot { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    // BR-ORD-21: tính lúc tạo đơn — khách lần đầu, tiền mặt từ ngưỡng, hoặc SĐT có cờ không đến lấy
    public bool NeedsContactConfirm { get; init; }

    public DateTime? ContactConfirmedAtUtc { get; set; }

    public Guid? ContactConfirmedByStaffId { get; set; }

    public string? CancelReason { get; set; }

    public OrderActor? CancelledByType { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public Guid? PaidByStaffId { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime? RefundedAtUtc { get; set; }

    public string? RefundNote { get; set; }

    public Guid? RefundedByStaffId { get; set; }

    // BR-ORD-18: hủy vì khách không đến lấy — đếm theo SĐT để bật bước gọi xác nhận cho đơn sau
    public bool NoPickup { get; set; }

    // Lần đầu đơn được đưa vào thư "cần xử lý" gửi quản lý (mỗi đơn chỉ báo một lần)
    public DateTime? AttentionNotifiedAtUtc { get; set; }

    public int PointsEarned { get; set; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<OrderItem> Items { get; init; } = [];

    // Bếp chỉ thấy đơn chờ khi đã qua bước gọi xác nhận hoặc không thuộc diện phải gọi (BR-ORD-21)
    public bool ReadyForKitchen => !NeedsContactConfirm || ContactConfirmedAtUtc is not null;

    // BR-ORD-16: đã thu tiền rồi hủy mà chưa ghi hoàn tiền
    public bool RefundPending => Status == OrderStatus.Cancelled && PaymentStatus == PaymentStatus.Paid && RefundedAtUtc is null;
}
