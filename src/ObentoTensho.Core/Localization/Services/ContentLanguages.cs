using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Core.Settings.Domain;

namespace ObentoTensho.Core.Localization.Services;

// BR-I18N-01, docs/srs/22 §4.2: chỉ ngôn ngữ đang bật (i18n.enabledLocales); giá trị lạ hoặc chưa bật ⇒ tiếng Việt,
// nên không sinh khóa cache mới theo dữ liệu người gửi
public sealed class ContentLanguages(ISettingsReader settings)
{
    public const string Source = "vi";
    public static readonly IReadOnlyList<string> Targets = ["en", "ja"];

    public IReadOnlyList<string> Enabled() => Parse(settings.Get(SettingKeys.I18nEnabledLocales));

    public string Resolve(string? requested)
    {
        var lang = requested?.Trim().ToLowerInvariant();
        return lang is not null && Targets.Contains(lang, StringComparer.Ordinal) && Enabled().Contains(lang, StringComparer.Ordinal) ? lang : Source;
    }

    public static IReadOnlyList<string> Parse(string? value) =>
        (value ?? Source).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToArray();
}
