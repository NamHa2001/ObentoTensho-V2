namespace ObentoTensho.Contracts.Dtos.Promotions;

/// <summary>Khuyến mãi cho trang quản trị; <c>Version</c> dùng làm ETag.</summary>
public sealed record AdminPromotionDto(
    int Id,
    string Title,
    string Slug,
    string? DescriptionHtml,
    DateOnly StartDate,
    DateOnly EndDate,
    bool PublicTeaser,
    bool IsActive,
    DateTime CreatedAtUtc,
    string Version);
