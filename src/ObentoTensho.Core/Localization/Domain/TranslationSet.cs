namespace ObentoTensho.Core.Localization.Domain;

// Bản dịch của một loại nội dung ở một ngôn ngữ; ghép theo từng trường, thiếu thì tiếng Việt (BR-I18N-07)
public sealed class TranslationSet
{
    public const string SourceLang = "vi";

    public static readonly TranslationSet Source = new(SourceLang, new Dictionary<int, ContentTranslation>());

    private readonly string _lang;
    private readonly IReadOnlyDictionary<int, ContentTranslation> _items;

    public TranslationSet(string lang, IReadOnlyDictionary<int, ContentTranslation> items)
    {
        _lang = lang;
        _items = items;
    }

    public LocalizedText Apply(int entityId, SourceText source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.Equals(_lang, SourceLang, StringComparison.Ordinal))
        {
            return new LocalizedText(source, SourceLang, IsFallback: false);
        }

        if (!_items.TryGetValue(entityId, out var t) || !t.IsDisplayable(source.Hash))
        {
            return new LocalizedText(source, SourceLang, IsFallback: true);
        }

        var text = new SourceText(
            t.Title ?? source.Title,
            t.Summary ?? source.Summary,
            t.BodyHtml ?? source.BodyHtml,
            t.CtaText ?? source.CtaText,
            t.MetaTitle ?? source.MetaTitle,
            t.MetaDescription ?? source.MetaDescription);
        var fallback = Lacks(t.Title, source.Title) || Lacks(t.Summary, source.Summary) || Lacks(t.BodyHtml, source.BodyHtml)
            || Lacks(t.CtaText, source.CtaText) || Lacks(t.MetaTitle, source.MetaTitle) || Lacks(t.MetaDescription, source.MetaDescription);
        return new LocalizedText(text, _lang, fallback);
    }

    private static bool Lacks(string? translated, string? source) => translated is null && source is not null;
}
