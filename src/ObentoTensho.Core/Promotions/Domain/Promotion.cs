namespace ObentoTensho.Core.Promotions.Domain;

// docs/srs/08 §6 Promotions. Ngày là ngày VN, hiệu lực tính khi truy vấn (BR-VCH-17); xóa mềm. Banner thêm ở phần ảnh.
public sealed class Promotion
{
    public int Id { get; private set; }

    public required string Title { get; set; }

    public required string Slug { get; set; }

    public string? DescriptionHtml { get; set; }

    public required DateOnly StartDate { get; set; }

    public required DateOnly EndDate { get; set; }

    public bool PublicTeaser { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? DeletedAtUtc { get; set; }

    public required Guid CreatedByStaffId { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; private set; } = [];

    public bool IsRunning(DateOnly today) => IsActive && DeletedAtUtc is null && StartDate <= today && today <= EndDate;

    public bool IsUpcoming(DateOnly today) => IsActive && DeletedAtUtc is null && PublicTeaser && today < StartDate;
}
