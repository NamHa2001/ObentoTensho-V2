using ObentoTensho.Contracts.Dtos.Invoices;
using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>
/// Khách đặt món mang về (FR-ORD-01). Giờ lấy là giờ Việt Nam. Không cần tài khoản; đang đăng nhập thì gắn vào tài khoản.
/// Giá, giảm giá, tổng tiền do server tính — mọi trường giá client gửi đều bị từ chối (BR-ORD-22).
/// <c>VoucherCode</c>: tối đa một mã mỗi đơn (BR-VCH-20); có voucher thì không giảm theo hạng (BR-VCH-04).
/// <c>Invoice</c>: khách cần xuất hóa đơn (cá nhân/doanh nghiệp); bỏ trống nếu không lấy hóa đơn.
/// <c>Channel</c> = <c>delivery</c>: giao tận nơi tới <c>DeliveryAddress</c>; phí ship server tính lại từ quãng đường, giờ chọn là giờ nhận hàng.
/// </summary>
public sealed record CreateOrderRequest(
    string CustomerName,
    string CustomerPhone,
    string CustomerEmail,
    DateOnly PickupDate,
    TimeOnly PickupTime,
    PaymentMethod PaymentMethod,
    IReadOnlyList<OrderLineRequest> Items,
    string? Note,
    string? Lang,
    string? VoucherCode = null,
    InvoiceBuyerRequest? Invoice = null,
    OrderChannel Channel = OrderChannel.Pickup,
    string? DeliveryAddress = null);
