using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>
/// Thẻ đơn trên màn hình bếp (FR-ORD-05, SEC-12): chỉ mã, tên gọi ngắn, giờ lấy, món + tùy chọn (tiếng Việt) và ghi chú —
/// không có SĐT, email, tiền.
/// </summary>
public sealed record KitchenOrderDto(
    Guid Id,
    string Code,
    OrderStatus Status,
    DateOnly PickupDate,
    TimeOnly PickupTime,
    string ShortName,
    IReadOnlyList<KitchenLineDto> Items,
    string? Note,
    string Version,
    OrderChannel Channel = OrderChannel.Pickup);
