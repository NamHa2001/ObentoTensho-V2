using ObentoTensho.Contracts.Dtos.Media;
using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Món trên trang thực đơn. Chỉ gồm món đang bán hoặc hết hàng (BR-MNU-02).</summary>
public sealed record PublicMenuItemDto(
    int Id,
    string Slug,
    int CategoryId,
    string Name,
    string? ShortDesc,
    decimal Price,
    decimal? OriginalPrice,
    SaleStatus Status,
    bool AllowTakeaway,
    MenuLabels Labels,
    IReadOnlyList<Allergen> Allergens,
    ImageDto? Image,
    string ContentLang,
    bool IsFallback);
