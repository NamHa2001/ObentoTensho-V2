namespace ObentoTensho.Core.Content.Domain;

// FR-CNT-04: slug thuộc danh sách cho phép; mỗi lần lưu tăng Version và ghi một dòng StaticPageVersions
public sealed class StaticPage
{
    public static readonly IReadOnlyList<string> AllowedSlugs = ["about", "contact", "privacy", "terms", "policy", "game-rules"];

    public int Id { get; private set; }

    public required string Slug { get; init; }

    public required string Title { get; set; }

    public required string ContentHtml { get; set; }

    public int Version { get; set; }

    public required Guid UpdatedByStaffId { get; set; }

    public required DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; private set; } = [];
}
