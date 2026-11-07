using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Tùy chọn cho trang quản trị.</summary>
public sealed record AdminOptionDto(
    int Id,
    int GroupId,
    string Name,
    decimal PriceDelta,
    bool IsDefault,
    SaleStatus Status,
    IReadOnlyList<Allergen> Allergens,
    int DisplayOrder,
    string Version);
