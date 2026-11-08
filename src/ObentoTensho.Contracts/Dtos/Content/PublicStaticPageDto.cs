namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>Trang tĩnh cho khách. Trang pháp lý bản en/ja là bản dịch tham khảo, bản tiếng Việt có giá trị (BR-I18N-12).</summary>
public sealed record PublicStaticPageDto(string Slug, string Title, string ContentHtml, DateTime UpdatedAtUtc, string ContentLang, bool IsFallback);
