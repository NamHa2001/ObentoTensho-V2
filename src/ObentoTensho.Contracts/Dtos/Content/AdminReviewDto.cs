using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Content;

public sealed record AdminReviewDto(
    int Id,
    string CustomerName,
    string OrderCode,
    int? MenuItemId,
    string? ItemName,
    int Rating,
    string? Comment,
    ReviewStatus Status,
    string? ReplyContent,
    DateTime? RepliedAtUtc,
    DateTime CreatedAtUtc,
    string Version);
