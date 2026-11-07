namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary><c>Revision</c>: số phiên bản nội dung. <c>Version</c>: ETag cho lần sửa kế tiếp.</summary>
public sealed record StaticPageDto(string Slug, string Title, string ContentHtml, int Revision, DateTime UpdatedAtUtc, string Version);
