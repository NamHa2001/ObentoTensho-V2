using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Content.Abstractions;

// BR-CNT-44: chỉ các cột cần cho danh sách, không nạp ContentHtml
public sealed record NewsListRow(
    int Id,
    string Title,
    string Slug,
    string? Summary,
    Guid? ThumbnailImageId,
    int? CategoryId,
    string AuthorDisplayName,
    NewsStatus Status,
    DateTime? PublishedAtUtc,
    DateTime? DeletedAtUtc,
    byte[] RowVersion);
