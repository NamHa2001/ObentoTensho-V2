namespace ObentoTensho.Core.Content.Domain;

// docs/srs/08 §7 Banners; ngày hiển thị là ngày VN, hết hạn tự ẩn khi đọc (BR-CNT-34)
public sealed class Banner
{
    public int Id { get; private set; }

    public required Guid ImageId { get; set; }

    public Guid? ImageMobileId { get; set; }

    public required string Title { get; set; }

    public string? Subtitle { get; set; }

    public string? CtaText { get; set; }

    public string? CtaLink { get; set; }

    public int DisplayOrder { get; set; }

    public DateOnly? ShowFromDate { get; set; }

    public DateOnly? ShowToDate { get; set; }

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; private set; } = [];

    public bool IsVisible(DateOnly today) =>
        IsActive && (ShowFromDate is not { } from || from <= today) && (ShowToDate is not { } to || today <= to);
}
