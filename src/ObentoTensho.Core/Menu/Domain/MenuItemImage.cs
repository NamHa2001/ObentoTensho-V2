namespace ObentoTensho.Core.Menu.Domain;

// docs/srs/08 §4 MenuItemImages: ảnh phụ của món, tối đa 4 (BR-MNU-06), xóa món ⇒ xóa liên kết
public sealed class MenuItemImage
{
    public const int MaxPerItem = 4;

    public int Id { get; private set; }

    public required int MenuItemId { get; init; }

    public required Guid ImageId { get; init; }

    public required int DisplayOrder { get; init; }
}
