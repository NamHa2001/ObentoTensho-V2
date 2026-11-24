using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.PlatformOrders;

// docs/srs/23 FR-PLT-01: nhập lại đơn khách đặt trên app; giảm giá nhập % hoặc số tiền (không cần cả hai)
public sealed record CreatePlatformOrderRequest(
    PaymentGroup PaymentGroup,
    Platform Platform,
    string? ExternalOrderCode,
    decimal? DiscountPercent,
    decimal? DiscountAmount,
    string? Note,
    IReadOnlyList<OrderLineRequest> Items);
