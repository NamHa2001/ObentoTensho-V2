namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Tạo/sửa danh mục. Đổi tên không đổi slug (BR-MNU-15); muốn đổi slug thì gửi <c>Slug</c>.</summary>
public sealed record CategoryRequest(string Name, string? Slug, bool IsActive, Guid? ImageId = null);
