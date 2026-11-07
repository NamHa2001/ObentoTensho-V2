using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Core.Content.Abstractions;

public sealed record NewsletterSendItem(NewsletterRecipient Recipient, NewsletterSubscriber Subscriber);
