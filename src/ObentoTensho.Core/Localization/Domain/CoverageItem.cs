using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Localization.Domain;

// Một mục đang hiển thị cho khách và tình trạng bản dịch của nó ở một ngôn ngữ (màn hình "Tiến độ dịch")
public sealed record CoverageItem(string Kind, string Ref, string CurrentHash, ContentTranslationState State)
{
    public static ContentTranslationState StateOf(string currentHash, string? translatedHash, TranslationStatus? status) =>
        translatedHash is null ? ContentTranslationState.Missing
        : !string.Equals(translatedHash, currentHash, StringComparison.Ordinal) ? ContentTranslationState.Outdated
        : status == TranslationStatus.Approved ? ContentTranslationState.Approved
        : ContentTranslationState.Draft;
}
