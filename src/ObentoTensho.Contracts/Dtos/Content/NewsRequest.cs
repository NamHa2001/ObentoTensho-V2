using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>
/// Tạo/sửa bài (FR-CNT-03). <c>PublishedAtUtc</c>: mốc đăng; <c>Published</c> với mốc tương lai là "đã lên lịch" (BR-CNT-40),
/// để trống khi xuất bản ⇒ đăng ngay. <c>Slug</c> trống ⇒ tự sinh; đổi slug bài đã đăng thì slug cũ chuyển hướng 301 (BR-CNT-41).
/// </summary>
public sealed record NewsRequest(
    string Title,
    string? Slug,
    Guid? ThumbnailImageId,
    string? Summary,
    string ContentHtml,
    int? CategoryId,
    string? AuthorDisplayName,
    string? MetaTitle,
    string? MetaDescription,
    Guid? OgImageId,
    NewsStatus Status,
    DateTime? PublishedAtUtc);
