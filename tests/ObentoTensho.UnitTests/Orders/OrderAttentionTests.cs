using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Orders.Domain;

namespace ObentoTensho.UnitTests.Orders;

// docs/srs/20 #30, BR-ORD-16/18: phân loại đơn "cần xử lý" (ngưỡng 30 phút chờ, 60 phút không đến lấy)
public sealed class OrderAttentionTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 5, 0, 0, DateTimeKind.Utc);

    [Theory]

    // Chờ xác nhận: tạo đã 30 phút, hoặc còn ≤ 30 phút tới giờ lấy
    [InlineData(OrderStatus.Pending, -30, 120, PaymentStatus.Unpaid, OrderAttention.Kind.PendingOverdue)]
    [InlineData(OrderStatus.Pending, -5, 30, PaymentStatus.Unpaid, OrderAttention.Kind.PendingOverdue)]
    [InlineData(OrderStatus.Pending, -5, 120, PaymentStatus.Unpaid, null)]

    // Sẵn sàng: quá giờ lấy 60 phút mới báo
    [InlineData(OrderStatus.Ready, -120, -60, PaymentStatus.Unpaid, OrderAttention.Kind.NotPickedUp)]
    [InlineData(OrderStatus.Ready, -120, -59, PaymentStatus.Unpaid, null)]
    [InlineData(OrderStatus.Preparing, -120, -90, PaymentStatus.Unpaid, null)]

    // Hủy sau khi đã thu tiền: không còn theo dõi hoàn tiền, quản lý tự xử lý (chủ quán 2026-11-26)
    [InlineData(OrderStatus.Cancelled, -120, 60, PaymentStatus.Paid, null)]
    [InlineData(OrderStatus.Cancelled, -120, 60, PaymentStatus.Unpaid, null)]
    [InlineData(OrderStatus.Completed, -120, -60, PaymentStatus.Paid, null)]
    public void Phan_loai_don_can_xu_ly(OrderStatus status, int createdMinutes, int pickupMinutes, PaymentStatus payment, OrderAttention.Kind? expected)
    {
        var order = new Order
        {
            OrderCode = "OBT-TK-0000-0000",
            DailySeq = 1,
            AccessTokenHash = new string('0', 64),
            CustomerName = "An",
            CustomerPhone = "+84912345678",
            CustomerEmail = "an@example.com",
            Lang = "vi",
            PickupAtUtc = Now.AddMinutes(pickupMinutes),
            SubTotal = 50_000,
            TotalAmount = 50_000,
            CreatedAtUtc = Now.AddMinutes(createdMinutes),
            Status = status,
            PaymentStatus = payment,
        };

        Assert.Equal(expected, OrderAttention.Classify(order, Now, pendingAlertMinutes: 30, noPickupMinutes: 60));
    }
}
