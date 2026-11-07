using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary><c>CanEdit</c>: còn sửa/xóa được (24 giờ đầu hoặc đang chờ duyệt — BR-CNT-16).</summary>
public sealed record MyReviewDto(
    int Id,
    Guid OrderId,
    int? MenuItemId,
    int Rating,
    string? Comment,
    ReviewStatus Status,
    string? ReplyContent,
    DateTime CreatedAtUtc,
    bool CanEdit);
