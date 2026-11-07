using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>Dòng danh sách bài cho quản trị, không có nội dung bài (BR-CNT-44). <c>DeletedAtUtc</c> khác null: trong thùng rác.</summary>
public sealed record AdminNewsListItemDto(
    int Id,
    string Title,
    string Slug,
    int? CategoryId,
    string AuthorDisplayName,
    NewsStatus Status,
    DateTime? PublishedAtUtc,
    DateTime? DeletedAtUtc,
    string Version);
