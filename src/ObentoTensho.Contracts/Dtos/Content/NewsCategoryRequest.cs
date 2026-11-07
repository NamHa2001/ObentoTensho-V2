namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>Tạo/sửa danh mục tin. <c>Slug</c> trống ⇒ tự sinh từ tên.</summary>
public sealed record NewsCategoryRequest(string Name, string? Slug, bool IsActive);
