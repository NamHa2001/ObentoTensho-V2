using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Media.Domain;

// docs/srs/08 §1 Images. OriginalPath là ảnh gốc không công khai; Version nằm trong đường dẫn biến thể (FR-IMG-04)
public sealed class StoredImage
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string OriginalPath { get; init; }

    public int Width { get; set; }

    public int Height { get; set; }

    public string? BlurHash { get; set; }

    public ImageStatus Status { get; set; } = ImageStatus.Processing;

    public string? VariantWidths { get; set; }

    public string Format { get; init; } = MediaRules.VariantFormat;

    public int Version { get; init; } = 1;

    public required string AltText { get; set; }

    public required string ContentHash { get; init; }

    // Đúng một trong hai: nhân viên tải ảnh nội dung, khách tải ảnh đại diện
    public Guid? CreatedByStaffId { get; init; }

    public Guid? CreatedByUserId { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    // FR-IMG-08: số lần xử lý nền thất bại liên tiếp; tự thử lại tối đa MediaRules.MaxProcessingAttempts lần
    public int ProcessingAttempts { get; set; }

    public DateTime? FailedAtUtc { get; set; }

    public IReadOnlyList<int> Widths =>
        string.IsNullOrEmpty(VariantWidths)
            ? []
            : VariantWidths.Split(',').Select(w => int.Parse(w, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
}
