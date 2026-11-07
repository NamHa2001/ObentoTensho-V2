namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary><c>ScheduledAtUtc</c> trống ⇒ gửi ngay khi bấm gửi.</summary>
public sealed record NewsletterCampaignRequest(string Subject, string ContentHtml, DateTime? ScheduledAtUtc);
