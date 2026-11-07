namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>Trang tĩnh trong danh sách quản trị; <c>Revision</c> = 0: chưa có nội dung, trang công khai trả 404.</summary>
public sealed record StaticPageSummaryDto(string Slug, string? Title, int Revision, DateTime? UpdatedAtUtc);
