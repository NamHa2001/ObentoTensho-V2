namespace ObentoTensho.Core.Content.Domain;

// Lịch sử nội dung trang tĩnh, khôi phục được
public sealed class StaticPageVersion
{
    public required int PageId { get; init; }

    public required int Version { get; init; }

    public required string Title { get; init; }

    public required string ContentHtml { get; init; }

    public required Guid UpdatedByStaffId { get; init; }

    public required DateTime UpdatedAtUtc { get; init; }
}
