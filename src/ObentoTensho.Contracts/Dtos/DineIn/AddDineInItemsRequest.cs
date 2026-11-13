using ObentoTensho.Contracts.Dtos.Orders;

namespace ObentoTensho.Contracts.Dtos.DineIn;

/// <summary>Một đợt gọi món gửi bếp. Giá do server tính lại (BR-ORD-22).</summary>
public sealed record AddDineInItemsRequest(IReadOnlyList<OrderLineRequest> Items);
