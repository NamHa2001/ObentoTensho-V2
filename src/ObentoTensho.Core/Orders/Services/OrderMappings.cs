using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Orders.Services;

// Thực thể → DTO (AR-08). Bản cho khách che liên hệ (BR-ORD-14); bản cho bếp không có liên hệ và tiền (SEC-12).
public static class OrderMappings
{
    public static PublicOrderDto ToPublic(this Order o, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var pickup = clock.ToLocal(o.PickupAtUtc);
        return new PublicOrderDto(
            o.OrderCode,
            o.Status,
            o.PaymentMethod,
            o.PaymentStatus,
            DateOnly.FromDateTime(pickup),
            TimeOnly.FromDateTime(pickup),
            o.CustomerName,
            PiiMask.Phone(o.CustomerPhone),
            PiiMask.Email(o.CustomerEmail),
            o.Items.Select(i => i.ToLineDto(localized: true)).ToArray(),
            o.SubTotal,
            o.MemberDiscount,
            o.VoucherDiscount,
            o.TotalAmount,
            o.Note,
            o.CanCustomerCancel(),
            o.CreatedAtUtc,
            o.Id,
            o.Channel,
            o.DeliveryAddress,
            o.DeliveryFee,
            o.ShippingDiscount);
    }

    public static AdminOrderDto ToAdmin(this Order o, IClock clock, bool noPickupFlag = false)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var pickup = clock.ToLocal(o.PickupAtUtc);
        return new AdminOrderDto(
            o.Id,
            o.OrderCode,
            o.Status,
            o.PaymentMethod,
            o.PaymentStatus,
            DateOnly.FromDateTime(pickup),
            TimeOnly.FromDateTime(pickup),
            o.CustomerName,
            PiiMask.Phone(o.CustomerPhone),
            PiiMask.Email(o.CustomerEmail),
            o.TotalAmount,
            o.Items.Sum(i => i.Quantity),
            o.NeedsContactConfirm,
            o.ContactConfirmedAtUtc is not null,
            o.RefundPending,
            noPickupFlag,
            o.CreatedAtUtc,
            RowVersions.Encode(o.RowVersion),
            o.Channel,
            o.PaymentGroup,
            o.Platform,
            o.ExternalOrderCode,
            o.DeletedAtUtc is not null);
    }

    public static AdminOrderDetailDto ToDetail(this Order o, IClock clock, bool noPickupFlag, IReadOnlyList<OrderStatusHistory> history) =>
        new(
            o.ToAdmin(clock, noPickupFlag),
            o.Items.Select(i => i.ToLineDto(localized: false)).ToArray(),
            o.SubTotal,
            o.MemberDiscount,
            o.VoucherDiscount,
            o.Note,
            o.CancelReason,
            o.PaidAtUtc,
            o.RefundedAtUtc,
            o.RefundNote,
            history.Select(h => new OrderHistoryDto(h.FromStatus, h.ToStatus, h.ChangedByType, h.ChangedById, h.Reason, h.ChangedAtUtc)).ToArray(),
            o.DeliveryAddress,
            o.DeliveryDistanceKm,
            o.DeliveryFee,
            o.ShippingDiscount,
            o.DeliveryLatitude,
            o.DeliveryLongitude,
            o.PlatformDiscount);

    public static KitchenOrderDto ToKitchen(this Order o, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var pickup = clock.ToLocal(o.PickupAtUtc);
        return new KitchenOrderDto(
            o.Id,
            o.OrderCode,
            o.Status,
            DateOnly.FromDateTime(pickup),
            TimeOnly.FromDateTime(pickup),
            ShortName(o.CustomerName),
            o.Items.Select(i => new KitchenLineDto(i.ItemName, i.Quantity, i.Options.Select(x => $"{x.GroupName}: {x.OptionName}").ToArray(), i.Note)).ToArray(),
            o.Note,
            RowVersions.Encode(o.RowVersion),
            o.Channel);
    }

    // BR-ORD-17: chỉ khi còn chờ; đơn phải gọi xác nhận mà đã gọi thì khách phải gọi quán để hủy
    public static bool CanCustomerCancel(this Order o) =>
        o.Status == OrderStatus.Pending && !(o.NeedsContactConfirm && o.ContactConfirmedAtUtc is not null);

    // Tên gọi ngắn cho bếp: tên riêng (từ cuối) của họ tên tiếng Việt
    public static string ShortName(string fullName)
    {
        ArgumentNullException.ThrowIfNull(fullName);
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? string.Empty : parts[^1];
    }

    public static OrderLineDto ToLineDto(this OrderItem i, bool localized) =>
        new(
            i.MenuItemId,
            localized ? i.ItemNameLocalized ?? i.ItemName : i.ItemName,
            i.ItemPrice,
            i.OptionsPrice,
            i.Quantity,
            i.LineTotal,
            i.Options.Select(x => new OrderOptionDto(
                localized ? x.GroupNameLocalized ?? x.GroupName : x.GroupName,
                localized ? x.OptionNameLocalized ?? x.OptionName : x.OptionName,
                x.PriceDelta)).ToArray(),
            i.Note);
}
