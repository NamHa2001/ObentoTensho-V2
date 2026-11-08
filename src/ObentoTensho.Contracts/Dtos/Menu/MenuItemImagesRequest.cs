namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Thay toàn bộ ảnh của món trong một lần (BR-MNU-12): ảnh chính và tối đa 4 ảnh phụ theo thứ tự. Mã ảnh lấy từ <c>POST /admin/images</c>.</summary>
public sealed record MenuItemImagesRequest(Guid? MainImageId, IReadOnlyList<Guid>? ImageIds);
