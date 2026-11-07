namespace ObentoTensho.Contracts.Dtos.Settings;

/// <summary>Cập nhật theo lô trong một giao dịch: một khóa sai thì không ghi khóa nào (BR-RPT-24).</summary>
public sealed record UpdateSettingsRequest(IReadOnlyDictionary<string, string> Values);
