using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Notifications.Domain;

// BR-NTF-06: nhật ký gửi — không lưu nội dung thư (có thể chứa token)
public sealed class EmailLog
{
    public long Id { get; private set; }

    public required string Type { get; init; }

    public required string ToEmail { get; init; }

    public EmailStatus Status { get; set; } = EmailStatus.Pending;

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public string? RelatedEntityId { get; init; }

    public required string IdempotencyKey { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? SentAtUtc { get; set; }
}
