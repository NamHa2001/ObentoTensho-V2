using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Localization;

/// <summary>
/// Bản dịch một nội dung. Trường nào loại nội dung không có thì luôn null (vd gallery chỉ có <c>Title</c> = chú thích).
/// <c>Outdated</c>: bản tiếng Việt đã đổi sau khi dịch — không hiển thị cho khách tới khi cập nhật (BR-I18N-02).
/// </summary>
public sealed record ContentTranslationDto(
    string Lang,
    string? Title,
    string? Summary,
    string? BodyHtml,
    string? CtaText,
    string? MetaTitle,
    string? MetaDescription,
    TranslationStatus Status,
    bool Outdated,
    DateTime UpdatedAtUtc);
