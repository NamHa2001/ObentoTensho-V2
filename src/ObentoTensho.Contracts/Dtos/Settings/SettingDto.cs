using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Settings;

/// <summary><c>Protected</c>: chỉ SuperAdmin sửa, cần step-up (BR-RPT-33).</summary>
public sealed record SettingDto(
    string Key,
    string Value,
    SettingType Type,
    decimal? MinValue,
    decimal? MaxValue,
    IReadOnlyList<string>? Choices,
    bool Protected,
    string Description,
    DateTime? UpdatedAtUtc);
