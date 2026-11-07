namespace ObentoTensho.Contracts.Dtos.Content;

public sealed record NewsletterSubscriberDto(int Id, string Email, string Lang, bool IsActive, DateTime? ConfirmedAtUtc, DateTime SubscribedAtUtc, DateTime? UnsubscribedAtUtc);
