using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Menu.Domain;

// docs/srs/08 §2 MenuItems. NameVi là bản gốc; bản dịch ở MenuItemTranslations. Ảnh thêm ở phần ảnh.
public sealed class MenuItem
{
    public int Id { get; private set; }

    public required int CategoryId { get; set; }

    public required string NameVi { get; set; }

    public required string Slug { get; set; }

    // DB-17: chữ thường bỏ dấu của tên Việt + bản dịch đã duyệt — tìm "com ga" ra "Cơm gà"
    public required string NameSearch { get; set; }

    public string? ShortDesc { get; set; }

    // Đã làm sạch HTML trước khi lưu (SEC-08)
    public string? FullDescHtml { get; set; }

    // Ảnh chính qua pipeline ảnh (BR-CNT-30)
    public Guid? MainImageId { get; set; }

    public required decimal Price { get; set; }

    public decimal? OriginalPrice { get; set; }

    public bool IsNew { get; set; }

    public bool IsBestseller { get; set; }

    public bool IsFeatured { get; set; }

    public bool IsVegetarian { get; set; }

    public bool IsSpicy { get; set; }

    public bool AllowTakeaway { get; set; } = true;

    public SaleStatus Status { get; set; } = SaleStatus.Available;

    public DateTime? SoldOutUntilUtc { get; set; }

    // Mã dị ứng ngăn cách dấu phẩy, vd "egg,milk" (BR-MNU-17)
    public string? Allergens { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public int DisplayOrder { get; set; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; private set; } = [];
}
