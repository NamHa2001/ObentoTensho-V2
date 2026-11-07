using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Tạo/sửa món. <c>NameVi</c> là bản gốc; bản dịch sửa riêng. <c>OptionGroupIds</c> theo thứ tự hiển thị (≤ 10).</summary>
public sealed record MenuItemRequest(
    int CategoryId,
    string NameVi,
    string? Slug,
    string? ShortDesc,
    string? FullDescHtml,
    decimal Price,
    decimal? OriginalPrice,
    MenuLabels Labels,
    bool AllowTakeaway,
    SaleStatus Status,
    IReadOnlyList<Allergen> Allergens,
    IReadOnlyList<int> OptionGroupIds);
