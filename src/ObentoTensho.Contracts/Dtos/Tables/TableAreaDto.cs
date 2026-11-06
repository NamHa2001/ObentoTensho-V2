namespace ObentoTensho.Contracts.Dtos.Tables;

/// <summary>Khu vực (tầng, sân vườn…). <c>Version</c> dùng làm ETag khi sửa.</summary>
public sealed record TableAreaDto(int Id, string Name, bool IsActive, string Version);
