using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Content.Domain;

public sealed class NewsletterRecipient
{
    public const int MaxAttempts = 3;

    public required int CampaignId { get; init; }

    public required int SubscriberId { get; init; }

    public NewsletterRecipientStatus Status { get; set; }

    public byte Attempts { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public string? Error { get; set; }
}
