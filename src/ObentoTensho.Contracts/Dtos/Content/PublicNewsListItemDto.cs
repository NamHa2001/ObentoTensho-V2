using ObentoTensho.Contracts.Dtos.Media;

namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>Thẻ bài trang tin: không có nội dung bài (BR-CNT-44), chỉ tên hiển thị tác giả (BR-CNT-43).</summary>
public sealed record PublicNewsListItemDto(
    string Slug,
    string Title,
    string? Summary,
    ImageDto? Thumbnail,
    string? CategorySlug,
    string AuthorDisplayName,
    DateTime PublishedAtUtc,
    string ContentLang,
    bool IsFallback);
