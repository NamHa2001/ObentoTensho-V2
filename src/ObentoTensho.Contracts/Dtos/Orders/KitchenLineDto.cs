namespace ObentoTensho.Contracts.Dtos.Orders;

/// <summary>Dòng món cho bếp: tên tiếng Việt, số lượng, tùy chọn ("Nhóm: Tùy chọn"), ghi chú chế biến.</summary>
public sealed record KitchenLineDto(string Name, int Quantity, IReadOnlyList<string> Options, string? Note);
