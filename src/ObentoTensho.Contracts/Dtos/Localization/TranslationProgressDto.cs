namespace ObentoTensho.Contracts.Dtos.Localization;

/// <summary>
/// Tiến độ dịch một ngôn ngữ (docs/srs/22 §4.3). <c>Ready</c>: đủ điều kiện bật (BR-I18N-06) xét phần dữ liệu;
/// <c>Blocking</c>: các mục còn thiếu dạng <c>loai:ma</c> (vd <c>menu_item:12</c>, <c>static_page:about</c>).
/// Khóa giao diện (XLF) do CI của frontend kiểm.
/// </summary>
public sealed record TranslationProgressDto(
    string Lang,
    bool Enabled,
    bool Ready,
    IReadOnlyList<TranslationKindProgressDto> Kinds,
    IReadOnlyList<string> Blocking);
