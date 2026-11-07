namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Một dòng giỏ: món, số lượng 1–99, các tùy chọn đã chọn, ghi chú chế biến. Không có trường giá — server tự tính (BR-ORD-22).</summary>
public sealed record OrderLineRequest(int MenuItemId, int Quantity, IReadOnlyList<int>? OptionIds, string? Note);
