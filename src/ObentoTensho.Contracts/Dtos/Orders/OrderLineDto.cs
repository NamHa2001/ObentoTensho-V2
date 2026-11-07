namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Dòng đơn đã chụp lại giá (BR-ORD-02): <c>LineTotal = (UnitPrice + OptionsPrice) × Quantity</c>.</summary>
public sealed record OrderLineDto(
    int? MenuItemId,
    string Name,
    decimal UnitPrice,
    decimal OptionsPrice,
    int Quantity,
    decimal LineTotal,
    IReadOnlyList<OrderOptionDto> Options,
    string? Note);
