using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Content.Domain;

// docs/srs/08 §7. Chỉ bản nháp sửa/xóa được; gửi do job đảm nhận (BR-CNT-23)
public sealed class NewsletterCampaign
{
    public int Id { get; private set; }

    public required string Subject { get; set; }

    public required string ContentHtml { get; set; }

    public Guid? SentByStaffId { get; set; }

    public DateTime? ScheduledAtUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? FinishedAtUtc { get; set; }

    public int RecipientCount { get; set; }

    public NewsletterCampaignStatus Status { get; set; }

    public required DateTime CreatedAtUtc { get; init; }

    public byte[] RowVersion { get; private set; } = [];
}
