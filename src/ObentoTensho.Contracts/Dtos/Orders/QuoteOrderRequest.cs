namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Kiểm lại giỏ hàng với giá và tình trạng hiện tại trước khi đặt (docs/srs/12 §4.1 bước 1).</summary>
public sealed record QuoteOrderRequest(IReadOnlyList<OrderLineRequest> Items, string? Lang);
