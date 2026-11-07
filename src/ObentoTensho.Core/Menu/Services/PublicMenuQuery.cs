using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Menu.Services;

// docs/srs/15 §3.3: lọc danh mục, tìm kiếm, bỏ món có chất dị ứng, chỉ món mang về, sắp xếp (danh sách cho phép), phân trang
public sealed record PublicMenuQuery(
    string? CategorySlug,
    string? Search,
    IReadOnlyList<Allergen>? ExcludeAllergens,
    bool? TakeawayOnly,
    string? Sort,
    int Page,
    int PageSize,
    string Lang);
