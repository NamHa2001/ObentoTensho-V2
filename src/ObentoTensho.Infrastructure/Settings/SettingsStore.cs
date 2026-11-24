using System.Collections.Frozen;
using System.Globalization;
using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Core.Settings.Domain;

namespace ObentoTensho.Infrastructure.Settings;

// Bản chụp bất biến, thay nguyên khối; tiến trình nền làm mới định kỳ để các instance khác thấy thay đổi
internal sealed class SettingsStore : ISettingsReader, ISettingsSnapshot
{
    private volatile FrozenDictionary<string, string> _values = FrozenDictionary<string, string>.Empty;

    public string Get(string key) =>
        _values.TryGetValue(key, out var value)
            ? value
            : SettingsCatalog.Find(key)?.Default ?? throw new InvalidOperationException($"Khóa cài đặt không có trong danh mục: {key}");

    public int GetInt(string key) => int.Parse(Get(key), CultureInfo.InvariantCulture);

    public decimal GetDecimal(string key) => decimal.Parse(Get(key), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    public bool GetBool(string key) => string.Equals(Get(key), "true", StringComparison.Ordinal);

    public IReadOnlyDictionary<string, string> Public() =>
        SettingsCatalog.All.Where(d => d.IsPublic).ToDictionary(d => d.Key, d => Get(d.Key), StringComparer.Ordinal);

    public void Replace(IReadOnlyDictionary<string, string> values) =>
        _values = values.ToFrozenDictionary(StringComparer.Ordinal);
}
