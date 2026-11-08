namespace ObentoTensho.Core.Menu.Domain;

// docs/srs/08 §2 MenuCategories. Ảnh đại diện thêm ở phần ảnh (bảng Images chưa có).
public sealed class MenuCategory
{
    public int Id { get; private set; }

    public required string Name { get; set; }

    public required string Slug { get; set; }

    public Guid? ImageId { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public required DateTime CreatedAtUtc { get; init; }

    public byte[] RowVersion { get; private set; } = [];
}
