using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Content;

public sealed record NewsletterCampaignDto(
    int Id,
    string Subject,
    string ContentHtml,
    NewsletterCampaignStatus Status,
    DateTime? ScheduledAtUtc,
    DateTime? StartedAtUtc,
    DateTime? FinishedAtUtc,
    int RecipientCount,
    int SentCount,
    int FailedCount,
    string Version);
