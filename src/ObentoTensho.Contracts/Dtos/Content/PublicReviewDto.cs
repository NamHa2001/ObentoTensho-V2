namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary><c>AuthorName</c> là tên rút gọn dạng "Nguyễn A." (BR-CNT-14).</summary>
public sealed record PublicReviewDto(
    int Id,
    int? MenuItemId,
    string AuthorName,
    int Rating,
    string? Comment,
    string? ReplyContent,
    DateTime? RepliedAtUtc,
    DateTime CreatedAtUtc);
