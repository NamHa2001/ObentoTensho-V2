using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Settings.Domain;

// Protected: chỉ SuperAdmin, step-up, báo mọi SuperAdmin (BR-RPT-33). IsPublic: trang khách đọc được
public sealed record SettingDefinition(
    string Key,
    SettingType Type,
    string Default,
    string Description,
    decimal? Min = null,
    decimal? Max = null,
    IReadOnlyList<string>? Choices = null,
    bool Protected = false,
    bool IsPublic = false);
