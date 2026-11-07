namespace ObentoTensho.Core.Settings.Abstractions;

public interface ISettingsSnapshot
{
    void Replace(IReadOnlyDictionary<string, string> values);
}
