namespace ObentoTensho.Contracts.Dtos.Localization;

/// <summary>
/// Bản dịch en/ja. Trường tương ứng bản Việt: khuyến mãi (Title, BodyHtml), banner (Title, Summary = phụ đề, CtaText),
/// gallery (Title = chú thích), tin tức (Title, Summary, BodyHtml, MetaTitle, MetaDescription), trang tĩnh (Title, BodyHtml).
/// <c>Approve</c> = người thạo ngôn ngữ đã đối chiếu với bản Việt (BR-I18N-03/04).
/// </summary>
public sealed record ContentTranslationRequest(
    string? Title,
    string? Summary,
    string? BodyHtml,
    string? CtaText,
    string? MetaTitle,
    string? MetaDescription,
    bool Approve);
