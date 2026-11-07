namespace ObentoTensho.Core.Menu.Domain;

// Món ↔ nhóm tùy chọn, có thứ tự hiển thị riêng của từng món
public sealed class MenuItemOptionGroup
{
    public required int MenuItemId { get; init; }

    public required int GroupId { get; init; }

    public int DisplayOrder { get; init; }
}
