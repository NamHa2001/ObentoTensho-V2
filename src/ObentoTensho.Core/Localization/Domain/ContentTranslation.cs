using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Localization.Domain;

// docs/srs/08 §2 "Bảng bản dịch": mỗi loại nội dung một bảng, PK (EntityId, Lang), chỉ en/ja.
// Trường nào loại nội dung không có thì để null và không ánh xạ cột.
public abstract class ContentTranslation
{
    public required int EntityId { get; init; }

    public required string Lang { get; init; }

    public string? Title { get; set; }

    public string? Summary { get; set; }

    public string? BodyHtml { get; set; }

    public string? CtaText { get; set; }

    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }

    public required string SourceHash { get; set; }

    public TranslationStatus ReviewStatus { get; set; }

    public Guid? TranslatedByStaffId { get; set; }

    public Guid? ReviewedByStaffId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public required DateTime UpdatedAtUtc { get; set; }

    // BR-I18N-02/03: chỉ bản đã duyệt và còn khớp bản Việt hiện tại
    public bool IsDisplayable(string currentSourceHash) =>
        ReviewStatus == TranslationStatus.Approved && string.Equals(SourceHash, currentSourceHash, StringComparison.Ordinal);

    public static ContentTranslation Create(TranslatableContent kind, int entityId, string lang, string sourceHash, DateTime nowUtc) => kind switch
    {
        TranslatableContent.Promotion => new PromotionTranslation { EntityId = entityId, Lang = lang, SourceHash = sourceHash, UpdatedAtUtc = nowUtc },
        TranslatableContent.Banner => new BannerTranslation { EntityId = entityId, Lang = lang, SourceHash = sourceHash, UpdatedAtUtc = nowUtc },
        TranslatableContent.Gallery => new GalleryTranslation { EntityId = entityId, Lang = lang, SourceHash = sourceHash, UpdatedAtUtc = nowUtc },
        TranslatableContent.News => new NewsTranslation { EntityId = entityId, Lang = lang, SourceHash = sourceHash, UpdatedAtUtc = nowUtc },
        _ => new StaticPageTranslation { EntityId = entityId, Lang = lang, SourceHash = sourceHash, UpdatedAtUtc = nowUtc },
    };
}
