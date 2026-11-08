using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Localization.Abstractions;
using ObentoTensho.Core.Localization.Domain;

namespace ObentoTensho.Core.Localization.Services;

// docs/srs/22 §4.2 "Đọc": trang công khai lấy bộ bản dịch của loại nội dung rồi ghép từng mục
public sealed class ContentLocalizer(ITranslationRepository translations, ITranslationCache cache, ContentLanguages languages)
{
    public async Task<TranslationSet> ForAsync(TranslatableContent kind, string? requestedLang, CancellationToken cancellationToken)
    {
        var lang = languages.Resolve(requestedLang);
        if (string.Equals(lang, ContentLanguages.Source, StringComparison.Ordinal))
        {
            return TranslationSet.Source;
        }

        var list = await cache.GetOrCreateAsync(kind, lang, ct => translations.ListAsync(kind, lang, ct), cancellationToken);
        return new TranslationSet(lang, list.ToDictionary(t => t.EntityId));
    }
}
