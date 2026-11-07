using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Core.Menu.Abstractions;

// Dữ liệu thực đơn đọc thẳng từ CSDL lúc đặt món (BR-ORD-02): món chưa xóa thuộc danh mục đang bật,
// liên kết món ↔ nhóm, các nhóm đó và mọi tùy chọn của nhóm (kể cả hết/ẩn — để báo lỗi đúng)
public sealed record OrderableMenu(
    IReadOnlyList<MenuItem> Items,
    IReadOnlyList<MenuItemOptionGroup> Links,
    IReadOnlyList<OptionGroup> Groups,
    IReadOnlyList<MenuOption> Options);
