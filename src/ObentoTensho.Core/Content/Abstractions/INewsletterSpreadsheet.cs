using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Core.Content.Abstractions;

public interface INewsletterSpreadsheet
{
    byte[] Write(IReadOnlyList<NewsletterSubscriber> subscribers);
}
