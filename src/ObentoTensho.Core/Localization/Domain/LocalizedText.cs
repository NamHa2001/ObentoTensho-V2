namespace ObentoTensho.Core.Localization.Domain;

// BR-I18N-07: ContentLang là ngôn ngữ thực tế; IsFallback khi có trường phải lấy tiếng Việt
public sealed record LocalizedText(SourceText Text, string ContentLang, bool IsFallback);
