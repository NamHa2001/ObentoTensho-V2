using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Tùy chọn công khai; <c>SoldOut</c> hiển thị mờ, không chọn được (BR-MNU-21).</summary>
public sealed record PublicOptionDto(int Id, string Name, decimal PriceDelta, bool IsDefault, SaleStatus Status, IReadOnlyList<Allergen> Allergens);
