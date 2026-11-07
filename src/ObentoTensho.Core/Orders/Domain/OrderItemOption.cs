namespace ObentoTensho.Core.Orders.Domain;

// BR-ORD-23: tùy chọn đã chọn, chụp lại tên nhóm/tùy chọn (tiếng Việt cho bếp, kèm bản theo ngôn ngữ khách) và giá cộng thêm
public sealed class OrderItemOption
{
    public long Id { get; private set; }

    public long OrderItemId { get; init; }

    public int? OptionId { get; init; }

    public required string GroupName { get; init; }

    public required string OptionName { get; init; }

    public string? GroupNameLocalized { get; init; }

    public string? OptionNameLocalized { get; init; }

    public required decimal PriceDelta { get; init; }
}
