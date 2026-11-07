using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Món cho trang quản trị.</summary>
public sealed record AdminMenuItemDto(
    int Id,
    int CategoryId,
    string NameVi,
    string Slug,
    string? ShortDesc,
    string? FullDescHtml,
    decimal Price,
    decimal? OriginalPrice,
    MenuLabels Labels,
    bool AllowTakeaway,
    SaleStatus Status,
    DateTime? SoldOutUntilUtc,
    IReadOnlyList<Allergen> Allergens,
    IReadOnlyList<int> OptionGroupIds,
    int DisplayOrder,
    string Version);
