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

    public decimal LineTotal => (ItemPrice + OptionsPrice) * Quantity;
}
