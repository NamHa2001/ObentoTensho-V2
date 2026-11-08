using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Core.Localization.Domain;

// Các trường văn bản của một nội dung (bản Việt hoặc bản dịch). Hash = băm bản Việt, đổi bất kỳ trường nào ⇒ bản dịch lỗi thời
public sealed record SourceText(
    string? Title,
    string? Summary = null,
    string? BodyHtml = null,
    string? CtaText = null,
    string? MetaTitle = null,
    string? MetaDescription = null)
{
    public string Hash => MenuRules.SourceHash(Title, Summary, BodyHtml, CtaText, MetaTitle, MetaDescription);
}
