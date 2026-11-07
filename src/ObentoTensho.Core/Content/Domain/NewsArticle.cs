using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Content.Domain;

// docs/srs/08 §7 News. Hẹn giờ tính khi truy vấn, không cần job (BR-CNT-01); AuthorDisplayName là tên công khai (BR-CNT-43)
public sealed class NewsArticle
{
    public const string DefaultAuthorName = "Obento Tensho";

    public int Id { get; private set; }

    public required string Title { get; set; }

    public required string Slug { get; set; }

    public required string TitleSearch { get; set; }

    public Guid? ThumbnailImageId { get; set; }

    public string? Summary { get; set; }

    public required string ContentHtml { get; set; }

    public int? CategoryId { get; set; }

    public Guid? AuthorId { get; init; }

    public string AuthorDisplayName { get; set; } = DefaultAuthorName;

    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    public Guid? OgImageId { get; set; }

    public NewsStatus Status { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsVisible(DateTime nowUtc) =>
        !IsDeleted && Status == NewsStatus.Published && PublishedAtUtc is { } at && at <= nowUtc;
}
