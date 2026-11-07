using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Customers.Domain;

// BR-CUS-07: tệp nằm trong CSDL tới khi hết hạn 24 giờ thì bị xóa, dòng còn lại làm bằng chứng
public sealed class DataExport
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required DataExportKind Kind { get; init; }

    public required Guid RequestedByStaffId { get; init; }

    public required string Reason { get; init; }

    public DataExportStatus Status { get; set; }

    public int RowCount { get; set; }

    public byte[]? Content { get; set; }

    public string? DownloadTokenHash { get; set; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? ExpiresAtUtc { get; set; }
}
