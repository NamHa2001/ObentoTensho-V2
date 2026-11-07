using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Menu.Domain;

// docs/srs/22: bản dịch en/ja. Mỗi loại thực thể một bảng *Translations, PK (EntityId, Lang) — lớp cơ sở này
// không ánh xạ riêng. SourceHash = băm bản tiếng Việt lúc dịch; khác bản hiện tại ⇒ lỗi thời, không hiển thị.
public abstract class MenuTranslation
{
    public required int EntityId { get; init; }

    public required string Lang { get; init; }

    public required string Name { get; set; }

    public string? ShortDesc { get; set; }

    public string? FullDescHtml { get; set; }

    public required string SourceHash { get; set; }

    public TranslationStatus ReviewStatus { get; set; }

    public Guid? TranslatedByStaffId { get; set; }

    public Guid? ReviewedByStaffId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public required DateTime UpdatedAtUtc { get; set; }

    public bool IsDisplayable(string currentSourceHash) =>
        ReviewStatus == TranslationStatus.Approved && string.Equals(SourceHash, currentSourceHash, StringComparison.Ordinal);
}
