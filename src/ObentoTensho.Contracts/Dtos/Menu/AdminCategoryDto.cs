namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Danh mục cho trang quản trị; <c>Version</c> dùng làm ETag.</summary>
public sealed record AdminCategoryDto(int Id, string Name, string Slug, int DisplayOrder, bool IsActive, int ItemCount, string Version);
