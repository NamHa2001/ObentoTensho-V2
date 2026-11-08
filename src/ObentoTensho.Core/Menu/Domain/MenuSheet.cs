namespace ObentoTensho.Core.Menu.Domain;

// BR-MNU-14: mẫu Excel thực đơn — tiêu đề cố định, tối đa 1.000 dòng và 2 MB. Món và danh mục nhận diện theo Slug:
// slug đã có ⇒ cập nhật, trống hoặc chưa có ⇒ tạo mới. Danh mục ghi trước nên món trong cùng tệp dùng được danh mục mới.
// Mô tả đầy đủ (HTML), ảnh, nhóm tùy chọn (cấu hình dùng chung, có quy tắc giá) và bản dịch sửa trên trang quản trị.
public static class MenuSheet
{
    public const int MaxRows = 1000;
    public const long MaxBytes = 2 * 1024 * 1024;

    public const string ItemsSheet = "Món";
    public const string CategoriesSheet = "Danh mục";
    public const string GuideSheet = "Hướng dẫn";

    public const string Slug = "Slug";
    public const string Category = "Danh mục";
    public const string Name = "Tên món";
    public const string ShortDesc = "Mô tả ngắn";
    public const string Price = "Giá";
    public const string OriginalPrice = "Giá gốc";
    public const string Status = "Trạng thái";
    public const string Takeaway = "Mang về";
    public const string IsNew = "Mới";
    public const string Bestseller = "Bán chạy";
    public const string Featured = "Nổi bật";
    public const string Vegetarian = "Chay";
    public const string Spicy = "Cay";
    public const string Allergens = "Dị ứng";
    public const string DisplayOrder = "Thứ tự";

    public const string CategoryName = "Tên danh mục";
    public const string CategoryVisible = "Đang hiện";

    public static readonly IReadOnlyList<string> Columns =
    [
        Slug, Category, Name, ShortDesc, Price, OriginalPrice, Status, Takeaway,
        IsNew, Bestseller, Featured, Vegetarian, Spicy, Allergens, DisplayOrder,
    ];

    public static readonly IReadOnlyList<string> CategoryColumns = [Slug, CategoryName, DisplayOrder, CategoryVisible];

    public const string Yes = "Có";
    public const string No = "Không";

    public static readonly IReadOnlyList<string> Guide =
    [
        "Mỗi dòng ở sheet \"Món\" là một món. Không đổi tên, thứ tự cột hay dòng tiêu đề.",
        "Slug: để trống để tạo món mới (hệ thống tự sinh). Điền slug của món đang có để cập nhật món đó.",
        "Danh mục: ghi slug danh mục, xem sheet \"Danh mục\".",
        "Sheet \"Danh mục\": mỗi dòng một danh mục. Slug đã có ⇒ cập nhật tên, thứ tự, trạng thái hiện; slug mới ⇒ tạo danh mục, món ở sheet \"Món\" ghi slug đó là dùng được ngay. Đang hiện để trống = Có.",
        "Giá, Giá gốc: số nguyên (đồng), ví dụ 65000. Giá gốc để trống hoặc lớn hơn Giá.",
        "Trạng thái: Đang bán, Hết hàng hoặc Ẩn. Để trống = Đang bán.",
        "Mang về, Mới, Bán chạy, Nổi bật, Chay, Cay: Có hoặc Không. Mang về để trống = Có, các cột còn lại để trống = Không.",
        "Dị ứng: các mã cách nhau dấu phẩy — egg (trứng), milk (sữa), soy (đậu nành), wheat (lúa mì), shellfish (giáp xác), fish (cá), peanut (đậu phộng), tree_nut (hạt cây), sesame (mè).",
        "Không dùng công thức. Tối đa 1.000 dòng, tệp tối đa 2 MB.",
        "Nhập luôn xem trước trước khi ghi; có một dòng lỗi thì không ghi dòng nào.",
        "Mô tả đầy đủ, ảnh, nhóm tùy chọn và bản dịch không có trong tệp này — sửa trên trang quản trị. Cập nhật bằng tệp giữ nguyên các phần đó.",
        "Không xóa danh mục hay món bằng tệp: dòng bị bỏ khỏi tệp được giữ nguyên.",
    ];
}
