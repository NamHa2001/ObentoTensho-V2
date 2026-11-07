using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Tạo/sửa tùy chọn; <c>PriceDelta</c> từ 0 đến 10.000.000 ₫ (BR-MNU-19).</summary>
public sealed record OptionRequest(string Name, decimal PriceDelta, bool IsDefault, SaleStatus Status, IReadOnlyList<Allergen> Allergens);
