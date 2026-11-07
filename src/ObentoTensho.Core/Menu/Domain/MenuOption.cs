using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Menu.Domain;

// Bảng Options (docs/srs/08 §2): giá cộng thêm ≥ 0 (BR-MNU-19), có trạng thái và dị ứng riêng (BR-MNU-24)
public sealed class MenuOption
{
    public int Id { get; private set; }

    public required int GroupId { get; init; }

    public required string Name { get; set; }

    public decimal PriceDelta { get; set; }

    public bool IsDefault { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Available;

    public string? Allergens { get; set; }

    public int DisplayOrder { get; set; }

    public byte[] RowVersion { get; private set; } = [];
}
