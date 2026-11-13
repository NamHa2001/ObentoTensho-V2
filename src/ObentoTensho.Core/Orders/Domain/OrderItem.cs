namespace ObentoTensho.Core.Orders.Domain;

// BR-ORD-02: chụp lại tên và giá lúc đặt. ItemName tiếng Việt (bếp đọc); ItemNameLocalized theo ngôn ngữ khách nếu khác.
// ItemPrice = giá món, OptionsPrice = Σ cộng thêm của tùy chọn cho 1 phần.
public sealed class OrderItem
{
    public long Id { get; private set; }

    public Guid OrderId { get; init; }

    public int? MenuItemId { get; init; }

    public required string ItemName { get; init; }

    public string? ItemNameLocalized { get; init; }

    public required decimal ItemPrice { get; init; }

    public decimal OptionsPrice { get; init; }

    public required int Quantity { get; init; }

    public string? Note { get; init; }

    public ICollection<OrderItemOption> Options { get; init; } = [];

    // Tại bàn: đợt gọi món (đơn online = 0), lúc gửi bếp, lúc bếp ra món, hủy món (lý do, người hủy)
    public int Round { get; init; }

    public DateTime? SentAtUtc { get; init; }

    public DateTime? ServedAtUtc { get; set; }

    public DateTime? VoidedAtUtc { get; set; }

    public string? VoidReason { get; set; }

    public Guid? VoidedByStaffId { get; set; }

    public bool IsVoided => VoidedAtUtc is not null;

    public decimal LineTotal => IsVoided ? 0 : (ItemPrice + OptionsPrice) * Quantity;
}
