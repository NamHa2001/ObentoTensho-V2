using ObentoTensho.Contracts.Dtos.Orders;

namespace ObentoTensho.Contracts.Dtos.Vouchers;

/// <summary>Giỏ hàng để liệt kê các mã khách chọn được ở bước thanh toán.</summary>
public sealed record VoucherOptionsRequest(IReadOnlyList<OrderLineRequest> Items, decimal? DeliveryFee = null);
