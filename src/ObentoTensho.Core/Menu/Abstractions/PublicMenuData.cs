using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Core.Menu.Abstractions;

// Ảnh chụp dữ liệu thực đơn công khai: danh mục đang bật, món chưa xóa và không ẩn, nhóm/tùy chọn đang dùng,
// liên kết món ↔ nhóm, bản dịch đã duyệt
public sealed record PublicMenuData(
    IReadOnlyList<MenuCategory> Categories,
    IReadOnlyList<MenuItem> Items,
    IReadOnlyList<OptionGroup> Groups,
    IReadOnlyList<MenuOption> Options,
    IReadOnlyList<MenuItemOptionGroup> ItemGroups,
    IReadOnlyList<MenuTranslation> Translations);
