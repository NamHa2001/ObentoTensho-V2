using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Localization.Domain;

namespace ObentoTensho.Core.Localization.Services;

// docs/srs/22 §4.2: tìm tin tức theo tiêu đề tiếng Việt (không dấu) và tiêu đề bản dịch đã duyệt, còn khớp bản Việt hiện hành
// — giống cách thực đơn tìm theo tên bản dịch. Bản dịch lỗi thời (bản Việt đã đổi) không được tìm thấy
public static class NewsSearchText
{
    public const int MaxLength = 400;

    public static string Build(NewsArticle article, IEnumerable<ContentTranslation> translations)
    {
        ArgumentNullException.ThrowIfNull(article);
        ArgumentNullException.ThrowIfNull(translations);
        var hash = ContentSources.Of(article).Hash;
        var titles = translations
            .Where(t => t.EntityId == article.Id && t.IsDisplayable(hash) && !string.IsNullOrWhiteSpace(t.Title))
            .OrderBy(t => t.Lang, StringComparer.Ordinal)
            .Select(t => t.Title);
        var text = TextNormalizer.Search([article.Title, .. titles]);
        return text.Length <= MaxLength ? text : text[..MaxLength];
    }
}
